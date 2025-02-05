#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using OxyPlot;
using OxyPlot.Avalonia;
using OxyPlot.Series;
using BarSeries = OxyPlot.Series.BarSeries;
using HorizontalAlignment = Avalonia.Layout.HorizontalAlignment;
using VerticalAlignment = Avalonia.Layout.VerticalAlignment;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class BiasPanel : UserControl
{
    private BiasPanel()
    {
        InitializeComponent();

        // Wait until this BiasPanel is attached to the visual tree so Grid layout can be based on it.
        this.Loaded += (_, _) => SetupGraphGrid();
    }

    public static async Task<BiasPanel> CreateAsync()
    {
        BiasPanel panel = new();

        PromptPanel promptPanel = await AwaitableTask.AwaitNotifyUi(PromptPanel.CreateAsync());
        panel.DockPanel.Children.Add(promptPanel);
        DockPanel.SetDock(promptPanel, Dock.Left);

        return panel;
    }

    private void SetupGraphGrid()
    {
        Grid graphGrid = new() { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(-16, 0, 0, 0) };

        graphGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (int i = 0; i < 6; i++)
        {
            if (i < 2)
                graphGrid.RowDefinitions.Add(new RowDefinition());

            graphGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        var separator = new Rectangle() { Width = 2, Height = this.Bounds.Height - 75, Margin = new Thickness(48, 0), VerticalAlignment = VerticalAlignment.Center, Fill = Brushes.BlueViolet };
        graphGrid.Children.Add(separator);
        Grid.SetRowSpan(separator, 2);

        for (int i = 0; i < 12; i++)
        {
            // TODO: docs page 54
            var plotModel = new PlotModel() { Title = $"Graph {i + 1}", PlotAreaBorderColor = OxyColors.Black, PlotAreaBorderThickness = new OxyThickness(1), Series = { new BarSeries() { ItemsSource = new List<BarItem>(new[] { new BarItem() { Value = 4 }, new BarItem() { Value = 9 }, new BarItem() { Value = 6 } }) } } };
            var plotView = new PlotView() { Model = plotModel };
            graphGrid.Children.Add(plotView);
            Grid.SetColumn(plotView, i < 6 ? i + 1 : i - 5); // i + 1 because the first column is reserved for the separator.
            Grid.SetRow(plotView, i < 6 ? 0 : 1);
        }

        this.DockPanel.Children.Add(graphGrid);
    }
}