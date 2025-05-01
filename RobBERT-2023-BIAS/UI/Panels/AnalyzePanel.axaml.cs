#region

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Conllu;
using Conllu.Enums;
using Microsoft.Extensions.DependencyInjection;
using RobBERT_2023_BIAS.Inference;
using RobBERT_2023_BIAS.Utilities;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class AnalyzePanel : UserControl
{
    private IRobbert _robbert2022 = null!;
    private IRobbert _robbert2023 = null!;

    private IStorageFile _parallelCorpus = null!;
    private IStorageFile _differentCorpus = null!;

    private readonly CancellationTokenSource _robbertCancellationSource = new();

    private AnalyzePanel()
    {
        InitializeComponent();
    }

    public static async Task<AnalyzePanel> CreateAsync()
    {
        var panel = new AnalyzePanel();

        await panel.InitializeAsync();

        return panel;
    }

    private async Task InitializeAsync()
    {
        var robbertFactory = App.ServiceProvider.GetRequiredService<IRobbertFactory>();

        _robbert2022 = await robbertFactory.Create(RobbertVersion.Base2022);
        _robbert2023 = await robbertFactory.Create(RobbertVersion.Base2023);
    }

    private async void SelectCorpus_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this) ?? throw new NullReferenceException();
        var selection = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "Select corpus file...",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("CoNLL-U corpus") { Patterns = ["*.conllu"] }],
        });

        if (selection.SingleOrDefault() is { } file)
        {
            if (sender is Button selectButton)
                if (selectButton.Name == nameof(ParallelCorpusButton))
                {
                    ParallelCorpusText.Text = file.Name;
                    _parallelCorpus = file;
                }
                else
                {
                    DifferentCorpusText.Text = file.Name;
                    _differentCorpus = file;
                }
            else
                throw new NullReferenceException();
        }
    }

    private async void StartAnalysis_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_parallelCorpus != null && _differentCorpus != null)
        {
            try
            {
                await TaskUtilities.AwaitNotify(this, AnalyzeEnglishBias());
            }
            catch (Exception ex)
            {
                ExceptionUtilities.LogNotify(this, ex);
            }
        }
        else
        {
            FlyoutBase.ShowAttachedFlyout(StartButton);
        }
    }

    private async Task AnalyzeEnglishBias()
    {
        // TODO: this MIGHT not be possible online
        var parallelSentences = ConlluParser.ParseFile(_parallelCorpus.Path.LocalPath).ToList();

        List<RobbertPrompt> parallelPrompts = new();
        List<string> parallelAuxiliaries = new();

        foreach (Sentence sentence in parallelSentences)
        {
            string auxForm = sentence.Tokens.First(t => t.DepRelEnum == DependencyRelation.Aux).Form;
            parallelPrompts.Add(new RobbertPrompt(sentence.RawTokenSequence(), auxForm));
            parallelAuxiliaries.Add(auxForm);
        }

        _robbert2022.BatchProgressChanged += ReportProgress;
        var processedParallelSentences2022 = await _robbert2022.ProcessBatch(parallelPrompts, 50, _robbertCancellationSource.Token, false);
        _robbert2022.BatchProgressChanged -= ReportProgress;

        _robbert2023.BatchProgressChanged += ReportProgress;
        var processedParallelSentences2023 = await _robbert2023.ProcessBatch(parallelPrompts, 50, _robbertCancellationSource.Token, false);
        _robbert2023.BatchProgressChanged -= ReportProgress;

        if (_robbertCancellationSource.IsCancellationRequested)
        {
            ConsoleWriteLine("Analysis canceled: exiting...");
            return;
        }

        var parallelLogits2022 = GetAuxiliaryLogits(processedParallelSentences2022, parallelAuxiliaries);
        var parallelLogits2023 = GetAuxiliaryLogits(processedParallelSentences2023, parallelAuxiliaries);


        var differentSentences = ConlluParser.ParseFile(_differentCorpus.Path.LocalPath).ToList();

        List<RobbertPrompt> differentPrompts = new();
        List<string> differentAuxiliaries = new();

        foreach (Sentence sentence in differentSentences)
        {
            string auxForm = sentence.Tokens.First(t => t.DepRelEnum == DependencyRelation.Aux).Form;
            differentPrompts.Add(new RobbertPrompt(sentence.RawTokenSequence(), auxForm));
            differentAuxiliaries.Add(auxForm);
        }

        _robbert2022.BatchProgressChanged += ReportProgress;
        var processedDifferentSentences2022 = await _robbert2022.ProcessBatch(differentPrompts, 50, _robbertCancellationSource.Token, false);
        _robbert2022.BatchProgressChanged -= ReportProgress;

        _robbert2023.BatchProgressChanged += ReportProgress;
        var processedDifferentSentences2023 = await _robbert2023.ProcessBatch(differentPrompts, 50, _robbertCancellationSource.Token, false);
        _robbert2023.BatchProgressChanged -= ReportProgress;

        if (_robbertCancellationSource.IsCancellationRequested)
        {
            ConsoleWriteLine("Analysis canceled: exiting...");
            return;
        }

        var differentLogits2022 = GetAuxiliaryLogits(processedDifferentSentences2022, differentAuxiliaries);
        var differentLogits2023 = GetAuxiliaryLogits(processedDifferentSentences2023, differentAuxiliaries);

        ConsoleText.Text =
            $"Bias ratio RobBERT2022 = {(parallelLogits2022.Sum() / parallelLogits2022.Count) / (differentLogits2022.Sum() / differentLogits2022.Count)}";
        ConsoleText.Text +=
            $"\nBias ratio RobBERT2023 = {(parallelLogits2023.Sum() / parallelLogits2023.Count) / (differentLogits2023.Sum() / differentLogits2023.Count)}";
    }

    private List<decimal> GetAuxiliaryLogits(List<List<Dictionary<string, float>>> processedSentences, List<string> auxiliaries)
    {
        // Decimal to avoid floating-point errors.
        List<decimal> logits = new();
        var logger = App.ServiceProvider.GetRequiredService<ILogSink>();
                    
        for (int i = 0; i < processedSentences.Count; i++)
        {
            if (!processedSentences[i].First().TryGetValue(auxiliaries[i], out float parallelAuxiliaryLogits) || processedSentences[i].Count > 1)
            {
                if (logger != null)
                    logger.Log(LogEventLevel.Warning, "NON-AVALONIA", this,
                        $"SKIPPED --- model: {processedSentences[i].First().Keys} (count: {processedSentences[i].Count}), aux: {auxiliaries[i]}");
            }
            else
            {
                logits.Add((decimal)parallelAuxiliaryLogits);
            }
        }

        return logits;
    }

    private void ReportProgress(object? sender, int progress) => Dispatcher.UIThread.Post(() => ConsoleText.Text = $"Processing... ({progress}%)");

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        try
        {
            _robbertCancellationSource.Cancel();
            
            _robbert2022.DisposeAsync();
            _robbert2023.DisposeAsync();
        }
        catch (Exception ex)
        {
            ExceptionUtilities.LogNotify(this, ex);
        }

        base.OnDetachedFromLogicalTree(e);
    }
}