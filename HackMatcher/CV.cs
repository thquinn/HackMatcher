using System;
using System.Collections.Generic;
using OpenCvSharp;
using Bitmap = System.Drawing.Bitmap;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace HackMatcher {
    public enum Suit { RED, BLACK, DIAMOND, CLUB, HEART, SPADE }
    public class Piece {
        public Suit suit;
        public int value;

        public override string ToString() {
            char s = "RBDCHS"[(int)suit];
            char v = value == 0 ? ' ' : "      6789X"[value];
            return "" + s + v;
        }
        public bool Color() {
            switch (suit) {
                case Suit.BLACK:
                    return false;
                case Suit.CLUB:
                    return false;
                case Suit.DIAMOND:
                    return true;
                case Suit.HEART:
                    return true;
                case Suit.RED:
                    return true;
                case Suit.SPADE:
                    return false;
            }
            return false;
        }
        public bool CanStackOn(Piece other) {
            if ((value == 0) != (other.value == 0)) {
                return false;
            }
            if (value == 0) {
                return suit == other.suit;
            }
            return (Color() != other.Color()) && (value == other.value - 1);
        }
    }

    public class CV {
        static Dictionary<Piece, Mat> TEMPLATES;
        static CV() {
            TEMPLATES = new Dictionary<Piece, Mat>();
            TEMPLATES.Add(new Piece() { suit = Suit.RED, value = 6 }, new Mat("templates/red6.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.RED, value = 7 }, new Mat("templates/red7.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.RED, value = 8 }, new Mat("templates/red8.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.RED, value = 9 }, new Mat("templates/red9.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.RED, value = 10 }, new Mat("templates/red10.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.BLACK, value = 6 }, new Mat("templates/black6.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.BLACK, value = 7 }, new Mat("templates/black7.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.BLACK, value = 8 }, new Mat("templates/black8.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.BLACK, value = 9 }, new Mat("templates/black9.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.BLACK, value = 10 }, new Mat("templates/black10.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.DIAMOND, value = 0 }, new Mat("templates/diamond.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.CLUB, value = 0 }, new Mat("templates/club.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.HEART, value = 0 }, new Mat("templates/heart.png"));
            TEMPLATES.Add(new Piece() { suit = Suit.SPADE, value = 0 }, new Mat("templates/spade.png"));
        }

        public static Piece[,] ReadBitmap(Bitmap bitmap) {
            Mat image = OpenCvSharp.Extensions.BitmapConverter.ToMat(bitmap);
            Piece[,] pieces = new Piece[9, 8];
            ConcurrentDictionary<Piece, List<Point>> points = new ConcurrentDictionary<Piece, List<Point>>();
            Parallel.ForEach(TEMPLATES.Keys, (type) => {
                points[type] = FindAll(image, type);
            });
            foreach (KeyValuePair<Piece, List<Point>> kvp in points) {
                foreach (Point point in kvp.Value) {
                    int x = point.X / 143;
                    int y = point.Y / 32;
                    pieces[x, y] = kvp.Key;
                }
            }
            Dictionary<Piece, int> pieceCounts = new Dictionary<Piece, int>();
            for (int x = 0; x < 9; x++) {
                for (int y = 0; y < 4; y++) {
                    Piece piece = pieces[x, y];
                    if (piece == null) {
                        return null;
                    }
                    if (!pieceCounts.ContainsKey(piece)) {
                        pieceCounts[piece] = 0;
                    }
                    pieceCounts[piece]++;
                    if (piece.value > 0 && pieceCounts[piece] > 2) {
                        return null;
                    }
                    if (piece.value == 0 && pieceCounts[piece] > 4) {
                        return null;
                    }
                }
            }
            return pieces;
        }
        private static List<Point> FindAll(Mat image, Piece type) {
            Mat result = image.MatchTemplate(TEMPLATES[type], TemplateMatchModes.CCoeffNormed);
            Mat.Indexer<float> indexer = result.GetGenericIndexer<float>();
            List<Point> points = new List<Point>();
            for (int x = 0; x < result.Cols; x++) {
                for (int y = 0; y < result.Rows; y++) {
                    if (indexer[y, x] > .9f) {
                        points.Add(new Point(x, y));
                    }
                }
            }
            return points;
        }
    }
}
