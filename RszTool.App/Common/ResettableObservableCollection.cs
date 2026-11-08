using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace RszTool.App.Common;

/// <summary>
/// Replaces a bound collection with one Reset notification. Source mutations must not run
/// inside ICollectionView.DeferRefresh: WPF selectors read CurrentPosition on notification.
/// </summary>
public sealed class ResettableObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> values)
    {
        var replacement = values.ToList();
        CheckReentrancy();
        Items.Clear();
        foreach (var value in replacement) Items.Add(value);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
