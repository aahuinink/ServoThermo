using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace temperatures
{
    public class LineDrawable : GraphData, IDrawable
    {
        private const int _graphCount = 3;
        private string[] _colorName = new string[_graphCount] { "FF0000", "00FF00", "0000FF" };
        private int[] _lineWidth = new int[_graphCount] { 1, 2, 3 };
        public GraphData tempGraph = new GraphData(0,0, Color.FromArgb("FF0000"), 2, true);

        // default constructor
        public LineDrawable() : base()
        {
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
                Rect lineGraphRect = new(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);
                DrawLineGraph(canvas, lineGraphRect, tempGraph);
        }
        private void DrawLineGraph(ICanvas canvas, Rect lineGraphRect, GraphData baseGraphData)
        {
            if (baseGraphData.Xaxis < 2)
            {
                baseGraphData.PointArray[baseGraphData.Xaxis] = baseGraphData.Yaxis;
                baseGraphData.Xaxis++;
                return;
            }
            else if (baseGraphData.Xaxis < 300)
            {
                baseGraphData.PointArray[baseGraphData.Xaxis] = baseGraphData.Yaxis;
                baseGraphData.Xaxis++;
                return;
            }
            else
            {
                for (int i = 0; i < 299; i++)
                {
                    baseGraphData.PointArray[i] = baseGraphData.PointArray[i + 1];
                }
                baseGraphData.PointArray[299] = baseGraphData.Yaxis;
            }
            for (int i = 0; i < baseGraphData.Xaxis - 1; i++)
            {
                canvas.StrokeColor = baseGraphData.LineColor;
                canvas.StrokeSize = baseGraphData.LineSize;
                canvas.DrawLine(i, baseGraphData.PointArray[i], i + 1, baseGraphData.PointArray[i + 1]);
            }
        }
    }
}
