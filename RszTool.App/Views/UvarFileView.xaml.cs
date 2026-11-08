using System.Windows.Controls;
using RszTool.App.ViewModels;

namespace RszTool.App.Views;

public partial class UvarFileView : UserControl
{
    public UvarFileView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is UvarFileViewModel old) old.RevealRow -= RevealRow;
            if (e.NewValue is UvarFileViewModel current) current.RevealRow += RevealRow;
        };
    }
    private void RevealRow(UvarFlagRow row)
    {
        FlagGrid.SelectedItem = row;
        FlagGrid.ScrollIntoView(row);
    }
    private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is UvarFileViewModel vm) vm.SelectionChanged(FlagGrid.SelectedItems.Cast<UvarFlagRow>().ToList());
    }
}
