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

    private IStorageFile? _parallelCorpus;
    private IStorageFile? _differentCorpus;

    private readonly CancellationTokenSource _robbertCancellationSource = new();

    private AnalyzePanel()
    {
        InitializeComponent();

        ProfileComboBox.Items.Insert((int)AnalyzeProfile.SubjectAuxiliary, "Subject-auxiliary inversion");
        ProfileComboBox.Items.Insert((int)AnalyzeProfile.VerbSecond, "Verb-second word order");
        ProfileComboBox.Items.Insert((int)AnalyzeProfile.PerfectParticiple, "Perfect participle position");
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
        if (_parallelCorpus != null && _differentCorpus != null && ProfileComboBox.SelectedIndex >= 0)
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
        var parallelSentences = ConlluParser.ParseFile(_parallelCorpus!.Path.LocalPath).ToList();

        List<RobbertPrompt> parallelPrompts = new();
        List<string> parallelProxies = new();

        foreach (Sentence sentence in parallelSentences)
        {
            string wordToDecode = sentence.Tokens.First(t => FilterMask(t, sentence)).Form;
            parallelProxies.Add(wordToDecode);

            // Crucially, the mask needs to be null here. We're not looking to predict words, we're looking at the logits for the KNOWN proxy.
            parallelPrompts.Add(new RobbertPrompt(sentence.RawTokenSequence(), null, wordToDecode));  
        }

        ConsoleWriteLine("Processing parallel corpus using RobBERT-2022-base (1/4)");

        _robbert2022.BatchProgressChanged += ReportProgress;
        var processedParallelSentences2022 = await _robbert2022.ProcessBatch(parallelPrompts, 5, _robbertCancellationSource.Token, false);
        _robbert2022.BatchProgressChanged -= ReportProgress;

        ConsoleWriteLine("Processing parallel corpus using RobBERT-2023-base (2/4)");

        _robbert2023.BatchProgressChanged += ReportProgress;
        var processedParallelSentences2023 = await _robbert2023.ProcessBatch(parallelPrompts, 5, _robbertCancellationSource.Token, false);
        _robbert2023.BatchProgressChanged -= ReportProgress;

        if (_robbertCancellationSource.IsCancellationRequested)
        {
            ConsoleWriteLine("Analysis canceled: exiting...");
            return;
        }

        var parallelLogits2022 = GetMaskLogits(processedParallelSentences2022, parallelProxies);
        var parallelLogits2023 = GetMaskLogits(processedParallelSentences2023, parallelProxies);


        var differentSentences = ConlluParser.ParseFile(_differentCorpus!.Path.LocalPath).ToList();

        List<RobbertPrompt> differentPrompts = new();
        List<string> differentProxies = new();

        foreach (Sentence sentence in differentSentences)
        {
            string wordToDecode = sentence.Tokens.First(t => FilterMask(t, sentence)).Form;
            differentProxies.Add(wordToDecode);

            differentPrompts.Add(new RobbertPrompt(sentence.RawTokenSequence(), null, wordToDecode));
        }

        ConsoleWriteLine("Processing different corpus using RobBERT-2022-base (3/4)");

        _robbert2022.BatchProgressChanged += ReportProgress;
        var processedDifferentSentences2022 = await _robbert2022.ProcessBatch(differentPrompts, 5, _robbertCancellationSource.Token, false);
        _robbert2022.BatchProgressChanged -= ReportProgress;

        ConsoleWriteLine("Processing different corpus using RobBERT-2023-base (4/4)");

        _robbert2023.BatchProgressChanged += ReportProgress;
        var processedDifferentSentences2023 = await _robbert2023.ProcessBatch(differentPrompts, 5, _robbertCancellationSource.Token, false);
        _robbert2023.BatchProgressChanged -= ReportProgress;

        if (_robbertCancellationSource.IsCancellationRequested)
        {
            ConsoleWriteLine("Analysis canceled: exiting...");
            return;
        }

        var differentLogits2022 = GetMaskLogits(processedDifferentSentences2022, differentProxies);
        var differentLogits2023 = GetMaskLogits(processedDifferentSentences2023, differentProxies);

        ConsoleWriteLine("Processing completed!");

        ConsoleWriteLine(
            $"Bias ratio RobBERT2022 = {(parallelLogits2022.Sum() / parallelLogits2022.Count) / (differentLogits2022.Sum() / differentLogits2022.Count)}");
        ConsoleWriteLine(
            $"Bias ratio RobBERT2023 = {(parallelLogits2023.Sum() / parallelLogits2023.Count) / (differentLogits2023.Sum() / differentLogits2023.Count)}");
    }

    private List<decimal> GetMaskLogits(List<List<Dictionary<string, float>>> processedSentences, List<string> masks)
    {
        // Decimal to avoid floating-point errors later.
        List<decimal> logits = new();
        var logger = App.ServiceProvider.GetRequiredService<ILogSink>();
                    
        for (int i = 0; i < processedSentences.Count; i++)
        {
            if (!processedSentences[i][0].TryGetValue(masks[i], out float maskLogits) || processedSentences[i].Count > 1) // TODO: caveat!
            {
                if (logger != null)
                    logger.Log(LogEventLevel.Warning, "NON-AVALONIA", this,
                        $"SKIPPED --- count: {processedSentences[i].Count}, aux: {masks[i]}");
            }
            else
            {
                logits.Add((decimal)maskLogits);
            }
        }

        return logits;
    }

    private bool FilterMask(Token token, Sentence sentence)
    {
        switch (ProfileComboBox.SelectedIndex)
        {
            case (int)AnalyzeProfile.SubjectAuxiliary:
                if (token.DepRelEnum == DependencyRelation.Aux)
                    return true;
                break;
            case (int)AnalyzeProfile.VerbSecond:
                if (token.DepRelEnum == DependencyRelation.Root)
                    return true;
                break;
            case (int)AnalyzeProfile.PerfectParticiple:
                if (sentence.Tokens.Any(t => t.DepRelEnum == DependencyRelation.Aux && t.Head == token.Id))
                    return true;
                break;
            default:
                throw new InvalidOperationException("Invalid analyze profile selected");
        }

        return false;
    }

    private void ReportProgress(object? sender, int progress)
    {
        if (progress <= 95) // Hack to avoid showing percentage after next step has already begun
            Dispatcher.UIThread.Post(() => ConsoleWriteLine($"Processing... ({progress}%)"));
    }

    private void ConsoleWriteLine(string text)
    {
        ConsoleText.Text += "\n" + text;
        ConsoleScrollViewer.ScrollToEnd();
    }

    private enum AnalyzeProfile
    {
        SubjectAuxiliary,
        VerbSecond,
        PerfectParticiple,
    }
    
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