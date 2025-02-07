#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using OxyPlot;
using OxyPlot.Avalonia;
using OxyPlot.Axes;
using OxyPlot.Series;
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

        // Wait until this BiasPanel is attached to the visual tree so Grid layout can be based on it.
        this.Loaded += (_, _) => SetupGraphGrid();
    }

    public static async Task<BiasPanel> CreateAsync()
    {
        BiasPanel panel = new();

        await panel.InitializeAsync();

        return panel;
    }

    private async Task InitializeAsync()
    {
        _biasPromptPanel = await TaskUtilities.AwaitNotifyUi(BiasPromptPanel.CreateAsync());

        DockPanel.Children.Add(_biasPromptPanel);
        DockPanel.SetDock(_biasPromptPanel, Dock.Left);

        // TODO: unsubscribe on disposal from all events everwhere
        _biasPromptPanel.OnModelOutput += (_, args) => CreateGraphs(args);
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

        var separator = new Rectangle() { Width = 2, Height = this.Bounds.Height - 75, Margin = new Thickness(48, 0), VerticalAlignment = VerticalAlignment.Center, Fill = Brushes.BlueViolet };
        _graphGrid.Children.Add(separator);
        Grid.SetRowSpan(separator, 2);

        this.DockPanel.Children.Add(_graphGrid);
    }

    private void CreateGraphs(BiasOutputEventArgs modelOutputs)
    {
        for (int token = 0; token < modelOutputs.FirstPrompt.Count + modelOutputs.SecondPrompt.Count; token++)
        {
            var barSource = new List<BarItem>();
            var axesSource = new List<string>();

            foreach (var tokenCandidate in token < modelOutputs.FirstPrompt.Count ? modelOutputs.FirstPrompt[token].Take(5).Reverse() : modelOutputs.SecondPrompt[token - modelOutputs.FirstPrompt.Count].Take(5).Reverse())
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

            var maximum = new List<Dictionary<string, float>>(modelOutputs.FirstPrompt).Concat(modelOutputs.SecondPrompt).SelectMany(d => d.Values).Max() * 100;

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
                Title = $"Prompt {(token < modelOutputs.FirstPrompt.Count ? "1" : "2")} - Token {(token < modelOutputs.FirstPrompt.Count ? token + 1 : token + 1 - modelOutputs.FirstPrompt.Count)}",
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
            Grid.SetColumn(plotView, token < modelOutputs.FirstPrompt.Count ? token + 1 : token + 1 - modelOutputs.FirstPrompt.Count); // token + 1 because the first column is reserved for the separator.
            Grid.SetRow(plotView, token < modelOutputs.FirstPrompt.Count ? 0 : 1);
        }
    }
}