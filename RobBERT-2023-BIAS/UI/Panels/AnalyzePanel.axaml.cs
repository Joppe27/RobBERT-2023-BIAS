#region

using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Conllu;
using Conllu.Enums;
using RobBERT_2023_BIAS.Inference;
using RobBERT_2023_BIAS.Utilities;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class AnalyzePanel : UserControl
{
    private Robbert _robbert2022 = null!;
    private Robbert _robbert2023 = null!;

    private IStorageFile _parallelCorpus = null!;
    private IStorageFile _differentCorpus = null!;

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
        _robbert2022 = await Robbert.CreateAsync(Robbert.RobbertVersion.Base2022);
        _robbert2023 = await Robbert.CreateAsync(Robbert.RobbertVersion.Base2023);
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
            await TaskUtilities.AwaitNotifyUi(this, AnalyzeEnglishBias());
        else
        {
            // TODO: show invalid corpus flyout
        }
    }

    private async Task AnalyzeEnglishBias()
    {
        var parallelSentences = ConlluParser.ParseFile(_parallelCorpus.Path.LocalPath).ToList();
        var differentSenteces = ConlluParser.ParseFile(_differentCorpus.Path.LocalPath).ToList();

        ConcurrentBag<float> robbert2022ParallelProbabilities = new();
        ConcurrentBag<float> robbert2023ParallelProbabilities = new();

        ConcurrentBag<float> robbert2022DifferentProbabilities = new();
        ConcurrentBag<float> robbert2023DifferentProbabilities = new();

        await Parallel.ForAsync(0, parallelSentences.Count, async (i, token) =>
        {
            Dispatcher.UIThread.Post(() => ConsoleText.Text = $"Processing \"parallel\" construction {i}...");

            var auxForm = parallelSentences[i].Tokens.First(t => t.DepRelEnum == DependencyRelation.Aux).Form;

            (await _robbert2022.Process(parallelSentences[i].RawTokenSequence(), 5, auxForm, false)).First()
                .TryGetValue(auxForm, out float robbert2022AuxProbability);
            robbert2022ParallelProbabilities.Add(robbert2022AuxProbability);

            (await _robbert2023.Process(parallelSentences[i].RawTokenSequence(), 5, auxForm, false)).First()
                .TryGetValue(auxForm, out float robbert2023AuxProbability);

            robbert2023ParallelProbabilities.Add(robbert2023AuxProbability);
        });

        await Parallel.ForAsync(0, differentSenteces.Count, async (i, token) =>
        {
            Dispatcher.UIThread.Post(() => ConsoleText.Text = $"Processing \"different\" construction {i}...");

            var auxForm = differentSenteces[i].Tokens.First(t => t.DepRelEnum == DependencyRelation.Aux).Form;

            (await _robbert2022.Process(differentSenteces[i].RawTokenSequence(), 5, auxForm, false)).First(d => d.ContainsKey(auxForm))
                .TryGetValue(auxForm, out float robbert2022AuxProbability);
            robbert2022DifferentProbabilities.Add(robbert2022AuxProbability);

            (await _robbert2023.Process(differentSenteces[i].RawTokenSequence(), 5, auxForm, false)).First(d => d.ContainsKey(auxForm))
                .TryGetValue(auxForm, out float robbert2023AuxProbability);

            robbert2023DifferentProbabilities.Add(robbert2023AuxProbability);
        });

        ConsoleText.Text =
            $"Bias ratio RobBERT2022 = {(robbert2022ParallelProbabilities.Sum() / robbert2022ParallelProbabilities.Count) / (robbert2022DifferentProbabilities.Sum() / robbert2022DifferentProbabilities.Count)}";
        ConsoleText.Text +=
            $"\nBias ration RobBERT2023 = {(robbert2023ParallelProbabilities.Sum() / robbert2023ParallelProbabilities.Count) / (robbert2023DifferentProbabilities.Sum() / robbert2023DifferentProbabilities.Count)}";
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        if (_robbert2022 != null)
            _robbert2022.Dispose();

        if (_robbert2023 != null)
            _robbert2023.Dispose();

        base.OnDetachedFromLogicalTree(e);
    }
}