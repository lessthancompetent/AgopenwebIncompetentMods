using System.ComponentModel;

namespace AgValoniaGPS.Models.Ntrip;

/// <summary>
/// Represents a field that can be associated with an NTRIP profile.
/// Used in the profile editor to show a list of fields with checkboxes.
/// </summary>
public class FieldAssociationItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string FieldName { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
