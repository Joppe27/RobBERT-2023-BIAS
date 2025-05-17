#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.LogicalTree;
using Avalonia.Media;
using OxyPlot;
using OxyPlot.Avalonia;
using OxyPlot.Axes;
using OxyPlot.Series;
using RobBERT_2023_BIAS.Inference;
using RobBERT_2023_BIAS.Utilities;
using BarSeries = OxyPlot.Series.BarSeries;
using CategoryAxis = OxyPlot.Axes.CategoryAxis;
using HorizontalAlignment = Avalonia.Layout.HorizontalAlignment;
using LogarithmicAxis = OxyPlot.Axes.LogarithmicAxis;
using VerticalAlignment = Avalonia.Layout.VerticalAlignment;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class BiasPanel : UserControl
{
    private BiasPromptPanel _biasPromptPanel = null!;
    private Grid _rightPanel = null!;
    private Grid _graphGrid = null!;

    private BiasPanel()
    {
        InitializeComponent();
    }

    public static async Task<BiasPanel> CreateAsync(RobbertVersion version)
    {
        BiasPanel panel = new();

        await panel.InitializeAsync(version);

        return panel;
    }

    private async Task InitializeAsync(RobbertVersion version)
    {
        _biasPromptPanel = await BiasPromptPanel.CreateAsync(version);

        DockPanel.Children.Add(_biasPromptPanel);
        DockPanel.SetDock(_biasPromptPanel, Dock.Left);

        DockPanel.Children.Add(_rightPanel = new Grid()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(-16, 0, 0, 0)
        });
        _rightPanel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _rightPanel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var separator = new Rectangle()
        {
            Width = 2,
            Margin = new Thickness(36, 36),
            VerticalAlignment = VerticalAlignment.Stretch,
            Fill = Brushes.BlueViolet,
        };
        _rightPanel.Children.Add(separator);
        Grid.SetColumn(separator, 0);

        var scrollViewer = new ScrollViewer()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
        };
        _rightPanel.Children.Add(scrollViewer);
        Grid.SetColumn(scrollViewer, 1);

        _graphGrid = new()
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 16)
        };
        scrollViewer.Content = _graphGrid;

        _graphGrid.RowDefinitions.AddRange(Enumerable.Range(0, 2).Select(_ => new RowDefinition()));

        _biasPromptPanel.OnModelOutput += CreateGraphs;
    }

    private void CreateGraphs(object? obj, BiasOutputEventArgs modelOutputs)
    {
        // Note: only tokens where 3/5 or more candidates are longer than 1 character are displayed as a graph because if not, 
        // the input token was probably a punctuation mark, space or special character. The model still processes these 
        // tokens so they are visible in its response in the prompt panel if necessary. For demonstration purposes only.
        var firstPromptTokens = modelOutputs.FirstPrompt
            .Where(d => d.Keys.Count(k => k.Trim().Length > 1) > 2).ToList();
        var secondPromptTokens = modelOutputs.SecondPrompt
            .Where(d => d.Keys.Count(k => k.Trim().Length > 1) > 2).ToList();

        _graphGrid.ColumnDefinitions.Clear();
        for (int i = 0; i < Math.Max(firstPromptTokens.Count, secondPromptTokens.Count); i++)
            _graphGrid.ColumnDefinitions.Add(new ColumnDefinition() { MinWidth = 250 });
        
        for (int token = 0; token < firstPromptTokens.Count + secondPromptTokens.Count; token++)
        {
            var barSource = new List<BarItem>();
            var axesSource = new List<string>();

            foreach (var tokenCandidate in token < firstPromptTokens.Count
                         ? firstPromptTokens[token].Take(5).Reverse()
                         : secondPromptTokens[token - firstPromptTokens.Count].Take(5).Reverse())
            {
                if (double.TryParse(MathUtilities.RoundSignificant(tokenCandidate.Value, 4), out double result))
                    barSource.Add(new BarItem() { Value = result != 0 ? result : double.Epsilon });
                else
                    throw new FormatException("Failed to parse string to double");

                axesSource.Add(tokenCandidate.Key);
            }

            var barSeries = new BarSeries()
            {
                ItemsSource = barSource,
                FillColor = OxyColors.BlueViolet,
                StrokeColor = OxyColors.MediumPurple,
                StrokeThickness = 1,
                LabelPlacement = LabelPlacement.Middle,
                LabelFormatString = "{0:g}%",
            };

            var maximum = new List<Dictionary<string, float>>(firstPromptTokens)
                .Concat(secondPromptTokens).SelectMany(d => d.Values).Max() * 100;

            var logAxis = new LogarithmicAxis()
            {
                Position = AxisPosition.Bottom,
                Minimum = 0.00000001,
                AbsoluteMinimum = 0.00000001,
                Maximum = maximum,
                AbsoluteMaximum = maximum,
            };

            var categoryAxis = new CategoryAxis()
            {
                Position = AxisPosition.Left,
                ItemsSource = axesSource,
                Angle = 90,
                IsZoomEnabled = false,
                IsPanEnabled = false,
            };

            var plotModel = new PlotModel()
            {
                Title = String.Format("Prompt {0} - Token {1}",
                    token < firstPromptTokens.Count ? "1" : "2",
                    token < firstPromptTokens.Count
                        ? modelOutputs.FirstPrompt.IndexOf(firstPromptTokens[token]) + 1
                        : modelOutputs.SecondPrompt.IndexOf(secondPromptTokens[token - firstPromptTokens.Count]) + 1),
                // This is a workaround because custom fonts are broken on WASM, see https://github.com/oxyplot/oxyplot/issues/2118 
                TitleFontSize = App.Current.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime ? 16 : 12,
                TitlePadding = 0,
                PlotAreaBorderColor = OxyColors.Black,
                PlotAreaBorderThickness = new OxyThickness(1),
                PlotMargins = new OxyThickness(24),
                Series = { barSeries },
                Axes = { categoryAxis, logAxis },
            };

            var plotView = new PlotView() { Model = plotModel };

            _graphGrid.Children.Add(plotView);

            Grid.SetColumn(plotView, token < firstPromptTokens.Count ? token : token - firstPromptTokens.Count);
            Grid.SetRow(plotView, token < firstPromptTokens.Count ? 0 : 1);
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        if (_biasPromptPanel != null)
            _biasPromptPanel.OnModelOutput -= CreateGraphs;

        base.OnDetachedFromLogicalTree(e);
    }
}