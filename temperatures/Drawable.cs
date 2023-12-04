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
        public GraphData[] lineGraphs = new GraphData[_graphCount];

        // default constructor
        public LineDrawable() : base()
        {
            for (int i = 0; i < _graphCount; i++)
            {
                lineGraphs[i] = new GraphData
                    (
                        yaxis: 0,
                        xaxis: 0,
                        lineColor: Color.FromArgb(_colorName[i]),
                        lineSize: _lineWidth[i],
                        newGraph: true
                    );
            }
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            for (int graphIndex = 0; graphIndex < lineGraphs.Length; graphIndex++)
            {
                Rect lineGraphRect = new(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);
                DrawLineGraph(canvas, lineGraphRect, lineGraphs[graphIndex]);
            }
        }

        private void DrawBarGraph(ICanvas canvas, Rect lineGraphRect, GraphData barGraph, int graphNumber)
        {
            int barWidth = 10;
            int lineGraphWidth = 1000;
            int barGraphLocation = lineGraphWidth + barWidth / 2 + graphNumber * barWidth;
            int graphHeight = 500;
            canvas.StrokeSize = barWidth;
            canvas.DrawLine(barGraphLocation, graphHeight, barGraphLocation, barGraph.Yaxis);
        }

        private void DrawLineGraph(ICanvas canvas, Rect lineGraphRect, GraphData baseGraphData)
        {
            if (baseGraphData.Xaxis < 2)
            {
                baseGraphData.PointArray[baseGraphData.Xaxis] = baseGraphData.Yaxis;
                baseGraphData.Xaxis++;
                return;
            }
            else if (baseGraphData.Xaxis < 1000)
            {
                baseGraphData.PointArray[baseGraphData.Xaxis] = baseGraphData.Yaxis;
                baseGraphData.Xaxis++;
                return;
            }
            else
            {
                for (int i = 0; i < 999; i++)
                {
                    baseGraphData.PointArray[i] = baseGraphData.PointArray[i + 1];
                }
                baseGraphData.PointArray[999] = baseGraphData.Yaxis;
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
