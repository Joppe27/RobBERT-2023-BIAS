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
using MathNet.Numerics.Statistics;
using Microsoft.Extensions.DependencyInjection;
using OxyPlot;
using OxyPlot.Avalonia;
using OxyPlot.Axes;
using OxyPlot.Series;
using RobBERT_2023_BIAS.Inference;
using RobBERT_2023_BIAS.Utilities;
using BarSeries = OxyPlot.Series.BarSeries;
using BoxPlotSeries = OxyPlot.Series.BoxPlotSeries;
using CategoryAxis = OxyPlot.Axes.CategoryAxis;
using LinearAxis = OxyPlot.Axes.LinearAxis;

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
                FlyoutBase.ShowAttachedFlyout(GraphGrid);
                
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
        var parallelFileStream = await _parallelCorpus!.OpenReadAsync();
        var parallelTextFile = await new StreamReader(parallelFileStream).ReadToEndAsync();
        var parallelSentences = ConlluParser.ParseText(parallelTextFile).ToList();

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


        var differentFileStream = await _differentCorpus!.OpenReadAsync();
        var differentTextFile = await new StreamReader(differentFileStream).ReadToEndAsync();
        var differentSentences = ConlluParser.ParseText(differentTextFile).ToList();

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

        (FlyoutBase.GetAttachedFlyout(GraphGrid) as Flyout)?.Hide();

        GraphGrid.Children.Clear();

        var allLogits = new List<List<double>>() { parallelLogits2022, parallelLogits2023, differentLogits2022, differentLogits2023 }.SelectMany(l => l)
            .ToList();
        var allBoxPlotsRange = new DataRange(allLogits.Min(), allLogits.Max());

        CreateBoxPlot(parallelLogits2022, parallelLogits2023, allBoxPlotsRange, "Parallel structure: logits distribution", 0);
        CreateBoxPlot(differentLogits2022, differentLogits2023, allBoxPlotsRange, "Different structure: logits distribution", 1);

        var ratio2022 = parallelLogits2022.Mean() / differentLogits2022.Mean();
        var ratio2023 = parallelLogits2023.Mean() / differentLogits2023.Mean();
        
        ConsoleWriteLine(
            $"Bias ratio RobBERT2022 = {ratio2022}");
        ConsoleWriteLine(
            $"Bias ratio RobBERT2023 = {ratio2023}");

        CreateBarPlot(ratio2022, ratio2023, "Parallel/different structure bias ratio", 3);
    }

    private List<double> GetMaskLogits(List<List<Dictionary<string, float>>> processedWords, List<string> masks)
    {
        // Decimal to avoid floating-point errors later.
        List<double> logits = new();
        var logger = App.ServiceProvider.GetRequiredService<ILogSink>();

        for (int i = 0; i < processedWords.Count; i++)
        {
            // If a word consists of multiple tokens, concatenate the highest logits tokens to form the word and use logits average of all tokens.
            if (processedWords[i].Count > 1)
            {
                var concatenatedWord = new Dictionary<string, float>();
                concatenatedWord.Add(String.Concat(processedWords[i].Select(t => t.Keys.First())),
                    processedWords[i].Select(t => t.Values.First()).Sum() / processedWords[i].Count);

                processedWords[i] = new List<Dictionary<string, float>>() { concatenatedWord };
            }

            if (processedWords[i][0].TryGetValue(masks[i], out float maskLogits))
            {
                logits.Add(maskLogits);
            }
            else
            {
                if (logger != null)
                    logger.Log(LogEventLevel.Warning, "NON-AVALONIA", this,
                        $"Word skipped during bias analysis - count: {processedWords[i].Count}, aux: {masks[i]}");
            }
        }

        return logits;
    }

    private void CreateBoxPlot(List<double> logits2022, List<double> logits2023, DataRange plotRange, string title, int gridColumn)
    {
        var boxplotSeries = new List<BoxPlotSeries>();

        var logitsList = new List<List<double>>() { logits2022, logits2023 };
        for (var i = 0; i < logitsList.Count; i++)
        {
            var lowerWhisker = logitsList[i].Where(l => l >= logitsList[i].LowerQuartile() - 1.5 * logitsList[i].InterquartileRange()).Min();
            var upperWhisker = logitsList[i].Where(l => l <= logitsList[i].UpperQuartile() + 1.5 * logitsList[i].InterquartileRange()).Max();

            boxplotSeries.Add(new BoxPlotSeries()
            {
                ItemsSource = new List<BoxPlotItem>
                {
                    new
                    (
                        i,
                        logitsList[i].Where(l => l >= logitsList[i].LowerQuartile() - 1.5 * logitsList[i].InterquartileRange()).Min(),
                        logitsList[i].LowerQuartile(),
                        logitsList[i].Median(),
                        logitsList[i].UpperQuartile(),
                        logitsList[i].Where(l => l <= logitsList[i].UpperQuartile() + 1.5 * logitsList[i].InterquartileRange()).Max()
                    )
                    {
                        Mean = logitsList[i].Mean(),
                        Outliers = logitsList[i].Where(l => l < lowerWhisker || l > upperWhisker).ToList(),
                    }
                },
                StrokeThickness = 2,
                Fill = i == 0 ? OxyColors.LightGreen : OxyColors.LightBlue,
            });
        }

        const int plotRangePadding = 1;
        var linearAxis = new LinearAxis()
        {
            Position = AxisPosition.Left,
            Minimum = plotRange.Minimum + plotRangePadding,
            Maximum = plotRange.Maximum + plotRangePadding,
        };

        var categoryAxis = new CategoryAxis()
        {
            Position = AxisPosition.Bottom,
            Minimum = -0.5,
            Maximum = 1.5,
            IsZoomEnabled = false,
            IsPanEnabled = false,
        };
        categoryAxis.Labels.AddRange(["RobBERT-2022-base", "RobBERT-2023-base"]);

        var plotModel = new PlotModel()
        {
            Title = title,
            TitleFontSize = 16,
            TitlePadding = 0,
            PlotAreaBorderColor = OxyColors.Black,
            PlotAreaBorderThickness = new OxyThickness(1),
            PlotMargins = new OxyThickness(24),
            Series = { boxplotSeries[0], boxplotSeries[1] },
            Axes = { categoryAxis, linearAxis },
        };

        var plotView = new PlotView() { Model = plotModel };

        GraphGrid.Children.Add(plotView);

        Grid.SetColumn(plotView, gridColumn);
        Grid.SetRow(plotView, 0);
    }

    private void CreateBarPlot(double ratio2022, double ratio2023, string title, int gridColumn)
    {
        var barSeries = new List<BarSeries>();

        var logitsList = new List<double>() { ratio2022, ratio2023 };
        for (var i = 0; i < logitsList.Count; i++)
        {
            barSeries.Add(new BarSeries()
            {
                ItemsSource = new List<BarItem> { new() { Value = logitsList[i], CategoryIndex = i } },
                FillColor = i == 0 ? OxyColors.LightGreen : OxyColors.LightBlue,
                LabelPlacement = LabelPlacement.Middle,
                StrokeThickness = 2,

                // See https://github.com/oxyplot/oxyplot/discussions/1946#discussioncomment-3806771.
                XAxisKey = "x",
                YAxisKey = "y",
            });
        }

        var linearAxis = new LinearAxis()
        {
            Position = AxisPosition.Left,
            Key = "x",
            Minimum = Math.Min(ratio2022, ratio2023) - Math.Abs(ratio2022 - ratio2023),
            Maximum = Math.Max(ratio2022, ratio2023) + Math.Abs(ratio2022 - ratio2023),
            AbsoluteMinimum = 0,
        };

        var categoryAxis = new CategoryAxis()
        {
            Position = AxisPosition.Bottom,
            IsZoomEnabled = false,
            IsPanEnabled = false,
            Key = "y",
        };
        categoryAxis.Labels.AddRange(["RobBERT-2022-base", "RobBERT-2023-base"]);

        var plotModel = new PlotModel()
        {
            Title = title,
            TitleFontSize = 16,
            TitlePadding = 0,
            PlotAreaBorderColor = OxyColors.Black,
            PlotAreaBorderThickness = new OxyThickness(1),
            PlotMargins = new OxyThickness(36, 24, 24, 24),
            Series = { barSeries[0], barSeries[1] },
            Axes = { categoryAxis, linearAxis },
        };

        var plotView = new PlotView() { Model = plotModel };

        GraphGrid.Children.Add(plotView);

        Grid.SetColumn(plotView, gridColumn);
        Grid.SetRow(plotView, 0);
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