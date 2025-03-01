#region

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using RobBERT_2023_BIAS.Inference;

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

    private async void SelectCorpus_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this) ?? throw new NullReferenceException();
        var selection = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "Select corpus file...",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.TextPlain],
        });

        if (selection.SingleOrDefault() is IStorageFile file)
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

    private void StartAnalysis_OnClick(object? sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private async Task InitializeAsync()
    {
        _robbert2022 = await Robbert.CreateAsync(Robbert.RobbertVersion.Base2022);
        _robbert2023 = await Robbert.CreateAsync(Robbert.RobbertVersion.Base2023);
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