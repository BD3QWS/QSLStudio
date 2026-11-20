using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QslStudio
{
    internal enum DrawingTool { Select, Line, Rectangle, Ellipse, Pen }

    internal static class CardDrawing
    {
        internal const int MaxPoints = 512;
        private const string Line = "draw-line", Frame = "draw-rectangle", Oval = "draw-ellipse", Pen = "draw-pen";

        internal static bool IsDrawing(Layer layer)
        {
            if (layer.Kind != LayerKind.Mark) return false;
            return layer.Symbol == Line || layer.Symbol == Frame || layer.Symbol == Oval || layer.Symbol == Pen;
        }

        internal static string Name(DrawingTool tool)
        {
            if (tool == DrawingTool.Line) return "线段";
            if (tool == DrawingTool.Rectangle) return "矩形";
            if (tool == DrawingTool.Ellipse) return "椭圆";
            return "手绘笔迹";
        }

        internal static Layer Create(DrawingTool tool, int side, Color color, IList<PointF> points)
        {
            float left = points[0].X, right = left, top = points[0].Y, bottom = top;
            foreach (var point in points)
            {
                left = Math.Min(left, point.X); right = Math.Max(right, point.X);
                top = Math.Min(top, point.Y); bottom = Math.Max(bottom, point.Y);
            }
            var layer = new Layer { Kind = LayerKind.Mark, Side = side, Color = color, Name = Name(tool),
                X = left, Y = top, W = Math.Max(1, right - left), H = Math.Max(1, bottom - top), Size = 3,
                Symbol = tool == DrawingTool.Line ? Line : tool == DrawingTool.Rectangle ? Frame : tool == DrawingTool.Ellipse ? Oval : Pen };
            if (tool == DrawingTool.Line || tool == DrawingTool.Pen)
            {
                layer.StrokePoints = new PointF[points.Count];
                for (int i = 0; i < points.Count; i++)
                    layer.StrokePoints[i] = new PointF((points[i].X - left) / layer.W, (points[i].Y - top) / layer.H);
            }
            return layer;
        }

        internal static void Paint(Graphics graphics, Layer layer)
        {
            using (var pen = new Pen(layer.Color, layer.Size) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (layer.Symbol == Frame) graphics.DrawRectangle(pen, layer.X, layer.Y, layer.W, layer.H);
                else if (layer.Symbol == Oval) graphics.DrawEllipse(pen, layer.Bounds);
                else if (layer.StrokePoints != null && layer.StrokePoints.Length >= 2)
                {
                    var points = new PointF[layer.StrokePoints.Length];
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new PointF(layer.X + layer.StrokePoints[i].X * layer.W, layer.Y + layer.StrokePoints[i].Y * layer.H);
                    graphics.DrawLines(pen, points);
                }
            }
        }
    }

    internal sealed partial class Canvas
    {
        private readonly List<PointF> sketch = new List<PointF>();
        private DrawingTool drawing;
        private Layer sketchPreview;
        private Color drawingInk;
        internal event Action<Layer> Drawn;
        internal bool IsDrawing { get { return drawing != DrawingTool.Select; } }

        internal void UseDrawingTool(DrawingTool tool)
        {
            DropSketch();
            drawing = tool;
            Cursor = IsDrawing ? Cursors.Cross : Cursors.Default;
            Invalidate();
        }

        private bool BeginSketch(Point location)
        {
            if (!IsDrawing) return false;
            if (!sheet.Contains(location.X, location.Y)) return true;
            drawingInk = Templates.DefaultInk(Card, Side);
            sketch.Add(ProjectPoint(location));
            Capture = true;
            return true;
        }

        private PointF ProjectPoint(Point location)
        {
            return new PointF(
                Math.Max(0, Math.Min(Card.Width, (location.X - sheet.X) / scale)),
                Math.Max(0, Math.Min(Card.Height, (location.Y - sheet.Y) / scale)));
        }

        private void ExtendSketch(Point location)
        {
            if (sketch.Count == 0) return;
            PointF point = ProjectPoint(location);
            PointF last = sketch[sketch.Count - 1];
            if (Math.Abs(last.X - point.X) + Math.Abs(last.Y - point.Y) < 1f) return;
            if (drawing == DrawingTool.Pen)
            {
                // Keep long freehand strokes editable at a bounded size.
                if (sketch.Count == CardDrawing.MaxPoints)
                {
                    for (int i = 1; i < sketch.Count / 2; i++) sketch[i] = sketch[i * 2];
                    sketch.RemoveRange(sketch.Count / 2, sketch.Count / 2);
                }
                sketch.Add(point);
            }
            else if (sketch.Count == 1) sketch.Add(point);
            else sketch[1] = point;
            sketchPreview = CardDrawing.Create(drawing, Side, drawingInk, sketch);
            Invalidate();
        }

        private bool FinishSketch(Point location)
        {
            if (!IsDrawing) return false;
            ExtendSketch(location);
            Layer completed = sketchPreview;
            DropSketch();
            if (Capture) Capture = false;
            if (completed != null && Drawn != null) Drawn(completed);
            Invalidate();
            return true;
        }

        private void DropSketch()
        {
            if (sketch.Count == 0) return;
            sketch.Clear(); sketchPreview = null;
            if (Capture) Capture = false;
            Invalidate();
        }

        private void PaintSketch(Graphics graphics)
        {
            if (sketchPreview == null) return;
            var state = graphics.Save();
            graphics.SetClip(sheet, CombineMode.Intersect);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(sheet.X, sheet.Y);
            graphics.ScaleTransform(scale, scale);
            CardDrawing.Paint(graphics, sketchPreview);
            graphics.Restore(state);
        }
    }
}
