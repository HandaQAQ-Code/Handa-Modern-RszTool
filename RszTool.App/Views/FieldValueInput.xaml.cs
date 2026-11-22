using System.ComponentModel;
﻿// for VectorBindingWrapper
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RszTool.App.Common;
using RszTool.App.ViewModels;

namespace RszTool.App.Views
{
    /// <summary>
    /// FieldValueInput.xaml 的交互逻辑
    /// </summary>
    public partial class FieldValueInput : UserControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(object), typeof(FieldValueInput),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSetValue));

        public static readonly DependencyProperty ValueTypeProperty =
            DependencyProperty.Register("ValueType", typeof(RszFieldType), typeof(FieldValueInput),
                new PropertyMetadata(RszFieldType.not_init, OnSetValueType));

        public static readonly DependencyProperty UpdateSourceProperty =
            DependencyProperty.Register("UpdateSource", typeof(bool), typeof(FieldValueInput),
                new PropertyMetadata(false));

        public static readonly DependencyProperty TypeNameProperty =
            DependencyProperty.Register("TypeName", typeof(string), typeof(FieldValueInput),
                new PropertyMetadata(null, OnEnumMetadataChanged));

        public static readonly DependencyProperty EnumDictProperty =
            DependencyProperty.Register("EnumDict", typeof(EnumDict), typeof(FieldValueInput),
                new PropertyMetadata(null, OnEnumMetadataChanged));

        public static readonly DependencyProperty EnumItemsProperty =
            DependencyProperty.Register(nameof(EnumItems), typeof(EnumItem[]), typeof(FieldValueInput), new PropertyMetadata(null));

        private static void OnEnumMetadataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (FieldValueInput)d;
            control.SetValue(EnumItemsProperty, control.TypeName == null ? null :
                (control.EnumDict?[control.TypeName] ?? control.EnumDict?[RszInstance.GetElementType(control.TypeName)])?.Items);
            if (control.ValueType != RszFieldType.not_init) control.ContentTemplate = SelectTemplate(control.ValueType, control);
        }

        // Register a custom routed event using the Bubble routing strategy.
        public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
            name: "ValueChanged",
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(FieldValueInput));

        public static readonly RoutedEvent ResourceChangedEvent = EventManager.RegisterRoutedEvent(
            name: "ResourceChanged",
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(FieldValueInput));

        private static DataTemplate SelectTemplate(RszFieldType fieldType, FrameworkElement element)
        {
            var resource = fieldType switch
            {
                RszFieldType.Bool => element.FindResource("InputBool"),
                RszFieldType.Vec2 or RszFieldType.Float2 or RszFieldType.Int2 or RszFieldType.Uint2
                    or RszFieldType.Point or RszFieldType.Size or RszFieldType.Range or RszFieldType.RangeI
                    => element.FindResource("InputVec2"),
                RszFieldType.Vec3 or RszFieldType.Float3 or RszFieldType.Int3 or RszFieldType.Uint3
                    => element.FindResource("InputVec3"),
                RszFieldType.Vec4 or RszFieldType.Float4 or RszFieldType.Int4 or RszFieldType.Uint4 or RszFieldType.Quaternion
                    => element.FindResource("InputVec4"),
                RszFieldType.Mat4 => element.FindResource("InputMat4"),
                RszFieldType.Guid or RszFieldType.GameObjectRef or RszFieldType.Uri => element.FindResource("InputGuid"),
                RszFieldType.Color => element.FindResource("InputColor"),
                RszFieldType.OBB => element.FindResource("InputOBB"),
                RszFieldType.Sphere => element.FindResource("InputSphere"),
                RszFieldType.AABB => element.FindResource("InputAABB"),
                RszFieldType.Capsule => element.FindResource("InputCapsule"),
                RszFieldType.Area => element.FindResource("InputArea"),
                RszFieldType.Data => element.FindResource("InputBytes"),
                _ => (element is FieldValueInput control && control.EnumItems != null) ?
                    element.FindResource("InputEnum") : element.FindResource("InputText"),
            };
            return (DataTemplate)resource;
        }

        private static void OnSetValue(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FieldValueInput control)
            {
                if (e.NewValue is Vector2 or Vector3 or Vector4 or Quaternion)
                {
                    if (control.Content is VectorBindingWrapper wrapper)
                    {
                        wrapper.UpdateFromValue(e.NewValue);
                    }
                    else
                    {
                        control.Content = new VectorBindingWrapper(control, e.NewValue);
                    }
                }
                else
                {
                    control.Content = e.NewValue;
                }
            }
        }

        private static void OnSetValueType(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FieldValueInput control)
            {
                control.ContentTemplate = SelectTemplate((RszFieldType)e.NewValue, control);
            }
        }

        public FieldValueInput()
        {
            InitializeComponent();
        }

        public RszFieldType ValueType
        {
            get { return (RszFieldType)GetValue(ValueTypeProperty); }
            set { SetValue(ValueTypeProperty, value); }
        }

        public object Value
        {
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public bool UpdateSource
        {
            get { return (bool)GetValue(UpdateSourceProperty); }
            set { SetValue(UpdateSourceProperty, value); }
        }

        public string? TypeName
        {
            get => GetValue(TypeNameProperty) as string;
            set => SetValue(TypeNameProperty, value);
        }

        public EnumDict? EnumDict
        {
            get => GetValue(EnumDictProperty) as EnumDict;
            set => SetValue(EnumDictProperty, value);
        }

        // Provide CLR accessors for assigning an event handler.
        public event RoutedEventHandler ValueChanged
        {
            add { AddHandler(ValueChangedEvent, value); }
            remove { RemoveHandler(ValueChangedEvent, value); }
        }

        public event RoutedEventHandler ResourceChanged
        {
            add { AddHandler(ResourceChangedEvent, value); }
            remove { RemoveHandler(ResourceChangedEvent, value); }
        }

        public EnumItem[]? EnumItems => (EnumItem[]?)GetValue(EnumItemsProperty);

        internal void RaiseValueChanged()
        {
            RoutedEventArgs routedEventArgs = new(routedEvent: ValueChangedEvent);
            RaiseEvent(routedEventArgs);
        }

        private void RaiseResourceChanged()
        {
            RoutedEventArgs routedEventArgs = new(routedEvent: ResourceChangedEvent);
            RaiseEvent(routedEventArgs);
        }

        private void OnBindingSourceUpdated(object sender, DataTransferEventArgs args)
        {
            RaiseValueChanged();
            if (Value is string path && RszUtils.IsResourcePath(path))
            {
                RaiseResourceChanged();
            }
            // if bind ValueType, force update source value
            if (UpdateSource)
            {
                GetBindingExpression(ValueProperty).UpdateSource();
            }
        }

        private void OnGuidNew(object sender, RoutedEventArgs e)
        {
            Value = Guid.NewGuid();
            RaiseValueChanged();
        }

        private void OnGuidChooseUvar(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
        {
            var owner = Window.GetWindow(this) ?? Application.Current.MainWindow;
            if (string.IsNullOrWhiteSpace(App.Instance.SaveData.UvarFilePath))
            {
                var settings = new UvarSettingsWindow { Owner = owner };
                if (settings.ShowDialog() != true) return;
            }
            var document = App.Instance.UvarWorkspace.RequireDocument();
            var picker = new UvarGuidPickerWindow(document, Value is Guid current ? current : Guid.Empty) { Owner = owner };
            if (picker.ShowDialog() != true || picker.ChosenFlag is not UvarFlagRow flag) return;
            // Keep the Value binding attached and commit through the normal field editor pipeline.
            SetCurrentValue(ValueProperty, flag.Variable.Id);
            GetBindingExpression(ValueProperty)?.UpdateSource();
            RaiseValueChanged();
            if (picker.MarkUsed)
            {
                string note = flag.Note;
                if (string.IsNullOrWhiteSpace(note) && DataContext is IFieldValueViewModel field) note = $"USER / {field.Name}";
                document.MarkChosen(flag, note);
            }
        });
    }


    [PropertyChanged.SuppressPropertyChangedWarnings]
    public class VectorBindingWrapper : INotifyPropertyChanged
    {
        private readonly FieldValueInput _control;
        private bool _isUpdating;
        private float _x;
        private float _y;
        private float _z;
        private float _w;
        private Type _valueType;

        public event PropertyChangedEventHandler? PropertyChanged;

        public VectorBindingWrapper(FieldValueInput control, object value)
        {
            _control = control;
            _valueType = value.GetType();
            ExtractValues(value);
        }

        public void UpdateFromValue(object value)
        {
            if (_isUpdating) return;
            _valueType = value.GetType();
            ExtractValues(value);
            NotifyAll();
        }

        private void ExtractValues(object value)
        {
            switch (value)
            {
                case Vector2 v2:
                    _x = v2.X; _y = v2.Y; _z = 0; _w = 0;
                    break;
                case Vector3 v3:
                    _x = v3.X; _y = v3.Y; _z = v3.Z; _w = 0;
                    break;
                case Vector4 v4:
                    _x = v4.X; _y = v4.Y; _z = v4.Z; _w = v4.W;
                    break;
                case Quaternion q:
                    _x = q.X; _y = q.Y; _z = q.Z; _w = q.W;
                    break;
            }
        }

        private void NotifyAll()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Y)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Z)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(W)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        public float X
        {
            get => _x;
            set { if (_x != value) { _x = value; CommitComponentChange(); } }
        }

        public float Y
        {
            get => _y;
            set { if (_y != value) { _y = value; CommitComponentChange(); } }
        }

        public float Z
        {
            get => _z;
            set { if (_z != value) { _z = value; CommitComponentChange(); } }
        }

        public float W
        {
            get => _w;
            set { if (_w != value) { _w = value; CommitComponentChange(); } }
        }

        public float this[int index]
        {
            get => index switch
            {
                0 => _x,
                1 => _y,
                2 => _z,
                3 => _w,
                _ => throw new IndexOutOfRangeException($"Index should be between 0 and 3, but got {index}"),
            };
            set
            {
                switch (index)
                {
                    case 0: if (_x != value) { _x = value; CommitComponentChange(); } break;
                    case 1: if (_y != value) { _y = value; CommitComponentChange(); } break;
                    case 2: if (_z != value) { _z = value; CommitComponentChange(); } break;
                    case 3: if (_w != value) { _w = value; CommitComponentChange(); } break;
                }
            }
        }

        private void CommitComponentChange()
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                object? newValue = null;
                if (_valueType == typeof(Vector2)) newValue = new Vector2(_x, _y);
                else if (_valueType == typeof(Vector3)) newValue = new Vector3(_x, _y, _z);
                else if (_valueType == typeof(Vector4)) newValue = new Vector4(_x, _y, _z, _w);
                else if (_valueType == typeof(Quaternion)) newValue = new Quaternion(_x, _y, _z, _w);

                if (newValue != null)
                {
                    _control.SetCurrentValue(FieldValueInput.ValueProperty, newValue);
                    _control.RaiseValueChanged();
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}
