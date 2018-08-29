// TODO
//      - Search is slow.
//          - Improve bottlenecks.
//          - Parallel While

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HackMatcher {
    class Program {
        public static IntPtr selfHandle;

        static void Main(string[] args) {
            // Run EXAPUNKS at 1366*768 resolution and disable HACK*MATCH CRT effect in the settings.
            // Launch HACK*MATCH, wait for the menu to show, then launch the solver.

            Process[] processes = Process.GetProcessesByName("EXAPUNKS");
            if (processes.Length == 0) {
                Console.WriteLine("Couldn't find an open instance of EXAPUNKS. Press any key to quit.");
                Console.ReadKey();
                return;
            }
            Process process = processes.OrderBy(e => e.StartTime).First();
            selfHandle = Process.GetCurrentProcess().MainWindowHandle;
            IntPtr hWnd = process.MainWindowHandle;
            if (hWnd != IntPtr.Zero) {
                Util32.handle = hWnd;
            }
            Util32.ForegroundWindow();

            while (true) {
                State state = null;
                Color heldColor = Color.White;
                while (state == null) {
                    //Image image = Image.FromFile("last.png");
                    Image image = ScreenCapture.CaptureWindow(hWnd);
                    Bitmap bitmap = new Bitmap(360, 540, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    using (Graphics g = Graphics.FromImage(bitmap)) {
                        Rectangle srcRect = new Rectangle(312, 110, 360, 540);
                        Rectangle destRect = new Rectangle(0, 0, 360, 540);
                        g.DrawImage(image, destRect, srcRect, GraphicsUnit.Pixel);
                    }
                    bitmap.Save("last.png");
                    state = CV.ReadBitmap(bitmap);
                    heldColor = new Bitmap(image).GetPixel(320, 610);
                }
                Console.WriteLine("Holding: " + state.held);
                List<Move> moves = FindMoves(state, out bool hasMatch);
                if (moves == null) {
                    continue;
                }
                
                foreach (Move move in moves) {
                    Console.WriteLine(move);
                }
                Util32.ExecuteMoves(new Queue<Move>(moves));
                if (hasMatch) {
                    Console.WriteLine("Found match, sleeping 500ms...");
                    Thread.Sleep(500);
                }
            }
        }

        static List<Move> FindMoves(State state, out bool hasMatch) {
            hasMatch = false;
            Console.WriteLine("Searching for a move...");
            Queue<State> queue = new Queue<State>();
            Dictionary<State, Tuple<State, Move>> parents = new Dictionary<State, Tuple<State, Move>>();
            queue.Enqueue(state);
            double maxEval = double.MinValue;
            State maxState = null;
            while (queue.Count > 0 && parents.Count < 25000) {
                State current = queue.Dequeue();
                Dictionary<Move, State> children = current.GetChildren();
                foreach (KeyValuePair<Move, State> child in children) {
                    if (parents.ContainsKey(child.Value)) {
                        continue;
                    }
                    parents.Add(child.Value, new Tuple<State, Move>(current, child.Key));
                    if (parents.Count % 25000 == 0) {
                        Console.WriteLine("Searched " + parents.Count + " states.");
                    }
                    queue.Enqueue(child.Value);
                    // Check eval.
                    double eval = child.Value.Eval();
                    eval -= parents.Count / 10000000f;
                    if (eval > maxEval) {
                        maxEval = eval;
                        maxState = child.Value;
                    }
                }
            }
            Console.WriteLine("Best eval: " + maxEval);
            List<Move> moves = new List<Move>();
            if (maxState == null) {
                moves.Add(new Move(Operation.GRAB_OR_DROP, 0));
                return moves;
            }
            while (parents.ContainsKey(maxState)) {
                Tuple<State, Move> parent = parents[maxState];
                moves.Add(parent.Item2);
                maxState = parent.Item1;
            }
            moves.Reverse();
            if (maxState.hasMatch) {
                hasMatch = true;
            }
            return moves;
        }
    }

    public enum PieceColor { RED, PINK, YELLOW, TEAL, PURPLE, UNKNOWN };
    public class Piece {
        public PieceColor color;
        public bool bomb;

        public Piece(PieceColor color, bool bomb) {
            this.color = color;
            this.bomb = bomb;
        }
        public Piece(Piece other) {
            this.color = other.color;
            this.bomb = other.bomb;
        }
        
        public override string ToString() {
            return color.ToString() + (bomb ? "!" : "");
        }
        public string ToString(bool abbrev) {
            return abbrev ? (int)color + (bomb ? "!" : "") : ToString();
        }
        public override bool Equals(object obj) {
            if (obj.GetType() != typeof(Piece))
                return false;
            Piece other = (Piece)obj;
            return ToString() == other.ToString();
        }
        public override int GetHashCode() {
            return ToString().GetHashCode();
        }
    }

    public class State {
        static int[][] NEIGHBORS = new int[][] { new int[] { -1, 0 }, new int[] { 1, 0 }, new int[] { 0, -1 }, new int[] { 0, 1 } };
        static StringBuilder sb = new StringBuilder();
        Piece[,] board;
        public Piece held;
        public bool hasMatch;
        private int hashCode;

        public State(Piece[,] board, Piece held) {
            this.board = board;
            this.held = held;
            hasMatch = false;
        }
        public State(State other) {
            board = (Piece[,])other.board.Clone();
            if (other.held != null) {
                held = new Piece(other.held);
            } else {
                held = null;
            }
            hasMatch = true;
        }
        private void CalculateHashCode() {
            unchecked {
                hashCode = 17;
                foreach (Piece piece in board) {
                    hashCode *= 31;
                    if (piece == null) {
                        continue;
                    }
                    hashCode += (int)piece.color;
                    hashCode *= 31;
                    hashCode += piece.bomb ? 1 : 0;
                }
                if (held != null) {
                    hashCode *= 31;
                    hashCode += (int)held.color;
                    hashCode *= 31;
                    hashCode += held.bomb ? 1 : 0;
                }
            }
        }

        public double Eval() {
            hasMatch = false;
            double eval = 0;
            HashSet<Tuple<int, int>> toCheck = new HashSet<Tuple<int, int>>();
            for (int x = 0; x < 7; x++) {
                for (int y = 0; y < board.GetLength(1); y++) {
                    if (board[x, y] == null) {
                        break;
                    }
                    toCheck.Add(new Tuple<int, int>(x, y));
                }
            }
            while (toCheck.Count > 3) {
                var enumerator = toCheck.GetEnumerator();
                enumerator.MoveNext();
                Tuple<int, int> start = enumerator.Current;
                int count = 1;
                bool match = false;
                Queue<Tuple<int, int>> queue = new Queue<Tuple<int, int>>();
                queue.Enqueue(start);
                toCheck.Remove(start);
                while (queue.Count > 0) {
                    Tuple<int, int> current = queue.Dequeue();
                    foreach (int[] coor in NEIGHBORS) {
                        Tuple<int, int> neighbor = new Tuple<int, int>(current.Item1 + coor[0], current.Item2 + coor[1]);
                        if (neighbor.Item1 < 0 || neighbor.Item1 >= 7) {
                            continue;
                        }
                        if (neighbor.Item2 < 0 || neighbor.Item2 >= board.GetLength(1)) {
                            continue;
                        }
                        if (!toCheck.Contains(neighbor)) {
                            continue;
                        }
                        if (board[neighbor.Item1, neighbor.Item2].color != board[start.Item1, start.Item2].color) {
                            continue;
                        }
                        if (board[neighbor.Item1, neighbor.Item2].bomb != board[start.Item1, start.Item2].bomb) {
                            continue;
                        }
                        count++;
                        if (count >= (board[start.Item1, start.Item2].bomb ? 2 : 4)) {
                            match = true;
                            hasMatch = true;
                        }
                        queue.Enqueue(neighbor);
                        toCheck.Remove(neighbor);
                    }
                }
                eval += count * count;
                if (match) {
                    eval += 1000;
                }
            }
            for (int x = 0; x < 7; x++) {
                int y = board.GetLength(1) - 1;
                while (y > 0 && board[x, y] == null) {
                    y--;
                }
                if (y > 3) {
                    eval -= 200 * Math.Pow(2, y - 3);
                }
            }
            return eval;
        }

        public Dictionary<Move, State> GetChildren() {
            Dictionary<Move, State> children = new Dictionary<Move, State>();
            // Grab/drop operations.
            for (int x = 0; x < 7; x++) {
                if (held == null) { // Grab.
                    if (board[x, 0] == null) {
                        continue;
                    }
                    int y = board.GetLength(1) - 1;
                    while (board[x, y] == null) {
                        y--;
                    }
                    State child = new State(this);
                    child.held = board[x, y];
                    child.board[x, y] = null;
                    children.Add(new Move(Operation.GRAB_OR_DROP, x), child);
                } else { // Drop.
                    if (board[x, board.GetLength(1) - 1] != null) {
                        continue;
                    }
                    int y = 0;
                    while (board[x, y] != null) {
                        y++;
                    }
                    State child = new State(this);
                    child.board[x, y] = held;
                    child.held = null;
                    children.Add(new Move(Operation.GRAB_OR_DROP, x), child);
                }
                // Swap.
                if (board[x, 1] == null) {
                    continue;
                }
                int swapY = board.GetLength(1) - 1;
                while (board[x, swapY] == null) {
                    swapY--;
                }
                State swapChild = new State(this);
                swapChild.board[x, swapY] = board[x, swapY - 1];
                swapChild.board[x, swapY - 1] = board[x, swapY];
                children.Add(new Move(Operation.SWAP, x), swapChild);
            }
            return children;
        }

        public override string ToString() {
            sb.Clear();
            for (int y = 0; y < board.GetLength(1); y++) {
                for (int x = 0; x < board.GetLength(0); x++) {
                    if (board[x,y] == null) {
                        sb.Append('_');
                        continue;
                    }
                    sb.Append(board[x, y].ToString(true));
                }
            }
            if (held != null) {
                sb.Append(held.ToString(true));
            }
            return sb.ToString();
        }

        public override int GetHashCode() {
            if (hashCode == 0) {
                CalculateHashCode();
            }
            return hashCode;
        }
    }

    public struct Move {
        public Operation operation;
        public int col;

        public Move(Operation operation, int col) {
            this.operation = operation;
            this.col = col;
        }

        public override string ToString() {
            return operation.ToString() + '@' + col;
        }
        public override int GetHashCode() {
            return (int)operation * 10 + col;
        }
    }

    public enum Operation { GRAB_OR_DROP, SWAP };
}
