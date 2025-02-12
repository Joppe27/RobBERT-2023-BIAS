#region

using Avalonia;
using Avalonia.Controls;
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
    private Grid _graphGrid = null!;
    
    private BiasPanel()
    {
        InitializeComponent();
    }

    public static async Task<BiasPanel> CreateAsync(Robbert.RobbertVersion version)
    {
        BiasPanel panel = new();

        await panel.InitializeAsync(version);

        return panel;
    }

    private async Task InitializeAsync(Robbert.RobbertVersion version)
    {
        _biasPromptPanel = await BiasPromptPanel.CreateAsync(version);

        DockPanel.Children.Add(_biasPromptPanel);
        DockPanel.SetDock(_biasPromptPanel, Dock.Left);
        SetupGraphGrid();

        _biasPromptPanel.OnModelOutput += CreateGraphs;
    }

    private void SetupGraphGrid()
    {
        _graphGrid = new() { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(-16, 0, 0, 0) };

        _graphGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (int i = 0; i < 6; i++)
        {
            if (i < 2)
                _graphGrid.RowDefinitions.Add(new RowDefinition());

            _graphGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        var separator = new Rectangle() { Width = 2, Margin = new Thickness(48, 36), VerticalAlignment = VerticalAlignment.Stretch, Fill = Brushes.BlueViolet };
        _graphGrid.Children.Add(separator);
        Grid.SetRowSpan(separator, 2);

        this.DockPanel.Children.Add(_graphGrid);
    }

    private void CreateGraphs(object? obj, BiasOutputEventArgs modelOutputs)
    {
        // Note: only tokens where 3/5 or more candidates are longer than 1 character are displayed as a graph because if not, 
        // the input token was probably a punctuation mark, space or special character. The model still processes these 
        // tokens so they are visible in its response in the prompt panel if necessary. For demonstration purposes only.
        var firstPromptTokens = modelOutputs.FirstPrompt.Where(d => d.Keys.Take(5).Count(k => k.Trim().Length > 1) > 2).ToList();
        var secondPromptTokens = modelOutputs.SecondPrompt.Where(d => d.Keys.Take(5).Count(k => k.Trim().Length > 1) > 2).ToList();

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
                    throw new Exception();

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

            var maximum = new List<Dictionary<string, float>>(firstPromptTokens).Concat(secondPromptTokens).SelectMany(d => d.Values).Max() * 100;

            var linearAxis = new LogarithmicAxis()
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
                Title = $"Prompt {(token < firstPromptTokens.Count ? "1" : "2")} - Token {(token < firstPromptTokens.Count ? modelOutputs.FirstPrompt.IndexOf(firstPromptTokens[token]) + 1 : modelOutputs.SecondPrompt.IndexOf(secondPromptTokens[token - firstPromptTokens.Count]) + 1)}",
                TitleFontSize = 16,
                TitlePadding = 0,
                PlotAreaBorderColor = OxyColors.Black,
                PlotAreaBorderThickness = new OxyThickness(1),
                PlotMargins = new OxyThickness(24),
                Series = { barSeries },
                Axes = { categoryAxis, linearAxis },
            };
            
            var plotView = new PlotView() { Model = plotModel };

            _graphGrid.Children.Add(plotView);
            Grid.SetColumn(plotView, token < firstPromptTokens.Count ? token + 1 : token + 1 - firstPromptTokens.Count); // token + 1 because the first column is reserved for the separator.
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