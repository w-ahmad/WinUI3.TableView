using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SampleApp;

#nullable disable

public class DataGridDataItem : INotifyDataErrorInfo, IComparable, INotifyPropertyChanged
{
    string _mountain;
    string _range;
    string _parentMountain;
    string _coordinates;
    string _ascents;
    uint _rank;
    uint _height;
    uint _prominence;
    DateTimeOffset _firstAscent;
    Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

    public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;
    public event PropertyChangedEventHandler PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        if (!string.IsNullOrEmpty(propertyName))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public uint Rank
    {
        get => _rank;
        set
        {
            if (_rank != value)
            {
                _rank = value;
                OnPropertyChanged();
            }
        }
    }

    public string Mountain
    {
        get => _mountain;
        set
        {
            if (_mountain != value)
            {
                _mountain = value;

                bool isMountainValid = !_errors.ContainsKey("Mountain");
                if (_mountain == string.Empty && isMountainValid)
                {
                    List<string> errors = new List<string>();
                    errors.Add("Mountain name cannot be empty");
                    _errors.Add("Mountain", errors);
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs("Mountain"));
                }
                else if (_mountain != string.Empty && !isMountainValid)
                {
                    _errors.Remove("Mountain");
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs("Mountain"));
                }

                OnPropertyChanged();
            }
        }
    }

    public uint Height_m
    {
        get => _height;
        set
        {
            if (_height != value)
            {
                _height = value;
                OnPropertyChanged();
            }
        }
    }

    public string Range
    {
        get => _range;
        set
        {
            if (_range != value)
            {
                _range = value;

                bool isRangeValid = !_errors.ContainsKey("Range");
                if (_range == string.Empty && isRangeValid)
                {
                    List<string> errors = new List<string>();
                    errors.Add("Range name cannot be empty");
                    _errors.Add("Range", errors);
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs("Range"));
                }
                else if (_range != string.Empty && !isRangeValid)
                {
                    _errors.Remove("Range");
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs("Range"));
                }

                OnPropertyChanged();
            }
        }
    }

    public string Parent_mountain
    {
        get => _parentMountain;
        set
        {
            if (_parentMountain != value)
            {
                _parentMountain = value;

                bool isParentValid = !_errors.ContainsKey("Parent_mountain");
                if (_parentMountain == string.Empty && isParentValid)
                {
                    List<string> errors = new List<string>();
                    errors.Add("Parent_mountain name cannot be empty");
                    _errors.Add("Parent_mountain", errors);
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs("Parent_mountain"));
                }
                else if (_parentMountain != string.Empty && !isParentValid)
                {
                    _errors.Remove("Parent_mountain");
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs("Parent_mountain"));
                }

                OnPropertyChanged();
            }
        }
    }

    public string Coordinates
    {
        get => _coordinates;
        set
        {
            if (_coordinates != value)
            {
                _coordinates = value;
                OnPropertyChanged();
            }
        }
    }

    public uint Prominence
    {
        get => _prominence;
        set
        {
            if (_prominence != value)
            {
                _prominence = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// You need to use DateTimeOffset to get proper binding to the CalendarDatePicker control, DateTime won't work.
    /// </summary>
    public DateTimeOffset First_ascent
    {
        get => _firstAscent; 
        set
        {
            if (_firstAscent != value)
            {
                _firstAscent = value;
                OnPropertyChanged();
            }
        }
    }

    public string Ascents
    {
        get => _ascents; 
        set
        {
            if (_ascents != value)
            {
                _ascents = value;
                OnPropertyChanged();
            }
        }
    }

    bool INotifyDataErrorInfo.HasErrors
    {
        get => _errors.Keys.Count > 0;
    }

    IEnumerable INotifyDataErrorInfo.GetErrors(string propertyName)
    {
        if (propertyName == null)
            propertyName = string.Empty;

        if (_errors.ContainsKey(propertyName))
            return _errors[propertyName];
        else
            return null;
    }

    int IComparable.CompareTo(object obj)
    {
        int lnCompare = Range.CompareTo((obj as DataGridDataItem).Range);

        if (lnCompare == 0)
            return Parent_mountain.CompareTo((obj as DataGridDataItem).Parent_mountain);
        else
            return lnCompare;
    }
}