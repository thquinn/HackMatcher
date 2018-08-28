using System;
using System.Collections.Generic;
using OpenCvSharp;
using Bitmap = System.Drawing.Bitmap;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace HackMatcher {
    public class CV {
        static Dictionary<Piece, Mat> TEMPLATES;
        static CV() {
            TEMPLATES = new Dictionary<Piece, Mat>();
            TEMPLATES.Add(new Piece(PieceColor.RED, false), new Mat("templates/red.png"));
            TEMPLATES.Add(new Piece(PieceColor.PINK, false), new Mat("templates/pink.png"));
            TEMPLATES.Add(new Piece(PieceColor.YELLOW, false), new Mat("templates/yellow.png"));
            TEMPLATES.Add(new Piece(PieceColor.TEAL, false), new Mat("templates/teal.png"));
            TEMPLATES.Add(new Piece(PieceColor.PURPLE, false), new Mat("templates/purple.png"));
            TEMPLATES.Add(new Piece(PieceColor.RED, true), new Mat("templates/red_bomb.png"));
            TEMPLATES.Add(new Piece(PieceColor.PINK, true), new Mat("templates/pink_bomb.png"));
            TEMPLATES.Add(new Piece(PieceColor.YELLOW, true), new Mat("templates/yellow_bomb.png"));
            TEMPLATES.Add(new Piece(PieceColor.TEAL, true), new Mat("templates/teal_bomb.png"));
            TEMPLATES.Add(new Piece(PieceColor.PURPLE, true), new Mat("templates/purple_bomb.png"));
        }

        public static State ReadBitmap(Bitmap bitmap) {
            Mat image = OpenCvSharp.Extensions.BitmapConverter.ToMat(bitmap);
            Piece[,] pieces = new Piece[7, 9];
            ConcurrentDictionary<Piece, List<Point>> points = new ConcurrentDictionary<Piece, List<Point>>();
            Parallel.ForEach(TEMPLATES.Keys, (type) => {
                points[type] = FindAll(image, type);
            });
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (List<Point> ps in points.Values) {
                foreach (Point p in ps) {
                    if (p.X < minX)
                        minX = p.X;
                    if (p.Y < minY)
                        minY = p.Y;
                }
            }
            Piece held = null;
            foreach (KeyValuePair<Piece, List<Point>> kvp in points) {
                Piece type = kvp.Key;
                foreach (Point point in kvp.Value) {
                    int x = (int)Math.Round((point.X - minX) / 51f);
                    int y = (int)Math.Round((point.Y - minY) / 51f);
                    if (y > 8) {
                        if (held != null) {
                            Console.WriteLine("Multiple held pieces detected.");
                        }
                        held = new Piece(type);
                        continue;
                    }
                    if (pieces[x, y] != null && !pieces[x, y].Equals(type)) {
                        Console.WriteLine("Different pieces found in same grid coordinate!");
                        return null;
                    }
                    pieces[x, y] = new Piece(type);
                }
            }
            // Check for holes in the grid.
            for (int x = 0; x < 7; x++) {
                bool piece = false;
                for (int y = pieces.GetLength(1) - 1; y >= 0; y--) {
                    if (pieces[x, y] != null) {
                        piece = true;
                    } else if (piece && pieces[x, y] == null) {
                        Console.WriteLine("Hole in grid at " + x + ", " + y + "!");
                        return null;
                    }
                }
            }
            return new State(pieces, held);
        }
        private static List<Point> FindAll(Mat image, Piece type) {
            Mat result = image.MatchTemplate(TEMPLATES[type], TemplateMatchModes.CCoeffNormed);
            Mat.Indexer<float> indexer = result.GetGenericIndexer<float>();
            List<Point> points = new List<Point>();
            for (int x = 0; x < result.Cols; x++) {
                for (int y = 0; y < result.Rows; y++) {
                    if (indexer[y, x] > .85f) {
                        points.Add(new Point(x, y));
                    }
                }
            }
            return points;
        }
    }
}
