using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Partcounter.Models;

public enum LabelElementType
{
    Text = 0,
    DataText = 1,
    QrCode = 2,
    Code128 = 3,
    Rectangle = 4,
    Line = 5
}

public enum LabelTextAlignment
{
    Left = 0,
    Center = 1,
    Right = 2
}

public sealed class LabelTemplateDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Neue Vorlage";
    public double WidthMm { get; set; } = 148;
    public double HeightMm { get; set; } = 105;
    public bool IsDefault { get; set; }
    public string? AssignedArticleNumber { get; set; }
    public List<LabelElementDefinition> Elements { get; set; } = new();
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public string AssignmentText => string.IsNullOrWhiteSpace(AssignedArticleNumber)
        ? "Standard / alle Artikel"
        : $"Artikel {AssignedArticleNumber}";

    public string DisplayName => IsDefault ? $"{Name} · STANDARD" : Name;

    public LabelTemplateDefinition DeepClone(string? newName = null)
    {
        return new LabelTemplateDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = newName ?? $"{Name} – Kopie",
            WidthMm = WidthMm,
            HeightMm = HeightMm,
            IsDefault = false,
            AssignedArticleNumber = AssignedArticleNumber,
            UpdatedAtUtc = DateTime.UtcNow,
            Elements = Elements.Select(e => e.DeepClone()).ToList()
        };
    }
}

public sealed class LabelElementDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public LabelElementType Type { get; set; }
    public double Xmm { get; set; }
    public double Ymm { get; set; }
    public double WidthMm { get; set; } = 40;
    public double HeightMm { get; set; } = 8;
    public string Content { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSizePt { get; set; } = 10;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public LabelTextAlignment Alignment { get; set; } = LabelTextAlignment.Left;
    public double BorderThickness { get; set; } = 1;
    public int ZIndex { get; set; }

    public LabelElementDefinition DeepClone() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Type = Type,
        Xmm = Xmm,
        Ymm = Ymm,
        WidthMm = WidthMm,
        HeightMm = HeightMm,
        Content = Content,
        FontFamily = FontFamily,
        FontSizePt = FontSizePt,
        Bold = Bold,
        Italic = Italic,
        Underline = Underline,
        Alignment = Alignment,
        BorderThickness = BorderThickness,
        ZIndex = ZIndex
    };
}

public sealed class LabelElementEditorRow : INotifyPropertyChanged
{
    private readonly LabelElementDefinition _model;

    public LabelElementEditorRow(LabelElementDefinition model) => _model = model;

    public LabelElementDefinition Model => _model;
    public string Id => _model.Id;

    public LabelElementType Type
    {
        get => _model.Type;
        set { if (_model.Type == value) return; _model.Type = value; Raise(); Raise(nameof(DisplayName)); }
    }

    public double Xmm { get => _model.Xmm; set => SetDouble(_model.Xmm, value, v => _model.Xmm = v); }
    public double Ymm { get => _model.Ymm; set => SetDouble(_model.Ymm, value, v => _model.Ymm = v); }
    public double WidthMm { get => _model.WidthMm; set => SetDouble(_model.WidthMm, Math.Max(1, value), v => _model.WidthMm = v); }
    public double HeightMm { get => _model.HeightMm; set => SetDouble(_model.HeightMm, Math.Max(1, value), v => _model.HeightMm = v); }

    public string Content
    {
        get => _model.Content;
        set { value ??= string.Empty; if (_model.Content == value) return; _model.Content = value; Raise(); Raise(nameof(DisplayName)); }
    }

    public string FontFamily
    {
        get => _model.FontFamily;
        set { value = string.IsNullOrWhiteSpace(value) ? "Segoe UI" : value.Trim(); if (_model.FontFamily == value) return; _model.FontFamily = value; Raise(); }
    }

    public double FontSizePt { get => _model.FontSizePt; set => SetDouble(_model.FontSizePt, Math.Clamp(value, 4, 96), v => _model.FontSizePt = v); }

    public bool Bold { get => _model.Bold; set { if (_model.Bold == value) return; _model.Bold = value; Raise(); } }
    public bool Italic { get => _model.Italic; set { if (_model.Italic == value) return; _model.Italic = value; Raise(); } }
    public bool Underline { get => _model.Underline; set { if (_model.Underline == value) return; _model.Underline = value; Raise(); } }
    public LabelTextAlignment Alignment { get => _model.Alignment; set { if (_model.Alignment == value) return; _model.Alignment = value; Raise(); } }
    public double BorderThickness { get => _model.BorderThickness; set => SetDouble(_model.BorderThickness, Math.Clamp(value, 0, 10), v => _model.BorderThickness = v); }
    public int ZIndex { get => _model.ZIndex; set { if (_model.ZIndex == value) return; _model.ZIndex = value; Raise(); } }

    public string DisplayName
    {
        get
        {
            var prefix = Type switch
            {
                LabelElementType.Text => "Text",
                LabelElementType.DataText => "Datenfeld",
                LabelElementType.QrCode => "QR",
                LabelElementType.Code128 => "Code128",
                LabelElementType.Rectangle => "Rahmen",
                LabelElementType.Line => "Linie",
                _ => Type.ToString()
            };
            var content = (Content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (content.Length > 28) content = content[..28] + "…";
            return string.IsNullOrWhiteSpace(content) ? prefix : $"{prefix}: {content}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyPositionChanged()
    {
        Raise(nameof(Xmm));
        Raise(nameof(Ymm));
    }

    private void SetDouble(double oldValue, double newValue, Action<double> setter, [CallerMemberName] string? propertyName = null)
    {
        if (Math.Abs(oldValue - newValue) < 0.0001) return;
        setter(newValue);
        Raise(propertyName);
    }

    private void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record LabelDataToken(string Token, string Description, string Example);
