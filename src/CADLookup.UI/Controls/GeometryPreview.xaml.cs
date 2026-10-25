using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CADLookup.Core.Domain;

namespace CADLookup.UI.Controls
{
    /// <summary>Draws host-neutral geometry figures in a canvas, auto-fitted with the Y axis flipped (drawing coordinates are Y up).</summary>
    public partial class GeometryPreview : UserControl
    {
        public static readonly DependencyProperty FiguresProperty = DependencyProperty.Register(
            nameof(Figures), typeof(IReadOnlyList<GeometryFigure>), typeof(GeometryPreview),
            new PropertyMetadata(null, (d, _) => ((GeometryPreview)d).Redraw()));

        public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
            nameof(EmptyText), typeof(string), typeof(GeometryPreview),
            new PropertyMetadata(string.Empty, (d, _) => ((GeometryPreview)d).ApplyEmptyText()));

        public GeometryPreview()
        {
            InitializeComponent();
            SizeChanged += (_, __) => Redraw();
        }

        public IReadOnlyList<GeometryFigure>? Figures
        {
            get => (IReadOnlyList<GeometryFigure>?)GetValue(FiguresProperty);
            set => SetValue(FiguresProperty, value);
        }

        public string EmptyText
        {
            get => (string)GetValue(EmptyTextProperty);
            set => SetValue(EmptyTextProperty, value);
        }

        private void ApplyEmptyText() => EmptyHint.Text = EmptyText;

        private void Redraw()
        {
            PreviewCanvas.Children.Clear();
            var figures = Figures;
            var bounds = ComputeBounds(figures);
            if (bounds == null)
            {
                EmptyHint.Visibility = Visibility.Visible;
                return;
            }
            EmptyHint.Visibility = Visibility.Collapsed;

            var (minX, minY, maxX, maxY) = bounds.Value;
            const double margin = 14;
            var availableWidth = Math.Max(ActualWidth - margin * 2, 1);
            var availableHeight = Math.Max(ActualHeight - margin * 2, 1);
            var width = Math.Max(maxX - minX, 1e-9);
            var height = Math.Max(maxY - minY, 1e-9);
            var scale = Math.Min(availableWidth / width, availableHeight / height);
            var offsetX = margin + (availableWidth - width * scale) / 2;
            var offsetY = margin + (availableHeight - height * scale) / 2;

            foreach (var figure in figures!)
            {
                var coordinates = figure.Coordinates;
                if (figure.Kind == GeometryFigure.PointsKind)
                {
                    for (var i = 0; i + 1 < coordinates.Count; i += 2)
                    {
                        var dot = new Ellipse { Width = 5, Height = 5 };
                        dot.SetResourceReference(Shape.FillProperty, "DarkPrimaryBrush");
                        SetCanvasPosition(dot, offsetX + (coordinates[i] - minX) * scale - 2.5,
                            offsetY + (maxY - coordinates[i + 1]) * scale - 2.5);
                        PreviewCanvas.Children.Add(dot);
                    }
                    continue;
                }
                if (coordinates.Count < 4) continue;
                var polyline = new Polyline
                {
                    StrokeThickness = 1.6,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                polyline.SetResourceReference(Shape.StrokeProperty, "DarkPrimaryBrush");
                for (var i = 0; i + 1 < coordinates.Count; i += 2)
                    polyline.Points.Add(new Point(
                        offsetX + (coordinates[i] - minX) * scale,
                        offsetY + (maxY - coordinates[i + 1]) * scale));
                if (figure.Closed && coordinates.Count >= 4)
                    polyline.Points.Add(polyline.Points[0]);
                PreviewCanvas.Children.Add(polyline);
            }
        }

        private void SetCanvasPosition(FrameworkElement element, double x, double y)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
        }

        private static (double MinX, double MinY, double MaxX, double MaxY)? ComputeBounds(IReadOnlyList<GeometryFigure>? figures)
        {
            double minX = 0, minY = 0, maxX = 0, maxY = 0;
            var hasPoint = false;
            foreach (var figure in figures ?? Array.Empty<GeometryFigure>())
            {
                var coordinates = figure.Coordinates;
                for (var i = 0; i + 1 < coordinates.Count; i += 2)
                {
                    var x = coordinates[i];
                    var y = coordinates[i + 1];
                    if (!hasPoint)
                    {
                        minX = maxX = x;
                        minY = maxY = y;
                        hasPoint = true;
                        continue;
                    }
                    if (x < minX) minX = x; else if (x > maxX) maxX = x;
                    if (y < minY) minY = y; else if (y > maxY) maxY = y;
                }
            }
            return hasPoint ? (minX, minY, maxX, maxY) : ((double, double, double, double)?)(null);
        }
    }
}
