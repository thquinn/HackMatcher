// TODO
//      - Is there a bug in search? We should never be grab/dropping consecutively in the same column.
//      - Search is slow.
//      - Reduce node limit when there are few tiles.
//      - Knock down tall columns.
//          - Work towards some heuristic
//      - Failure handling.
//          - Don't make moves while there are unknown tiles or holes in the grid.
//          - If an input gets dropped, redetermine grabbed position and drop any held tile.
//      - Hold K while there are no tall columns.
//      - Ponder moves while executing the previous set.

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
            // Set EXAPUNKS to 1366*768 resolution and disable HACK*MATCH CRT effect.
            // Launch HACK*MATCH and start a game, then launch the solver.
            // Each tile is 51px apart.

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

            int grabberCol = 3;
            Piece lastHeld = null;
            while (true) {
                Piece[,] board = null;
                while (board == null) {
                    //Image image = Image.FromFile("last.png");
                    Image image = ScreenCapture.CaptureWindow(hWnd);
                    Bitmap bitmap = new Bitmap(364, 436, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    image.Save("last.png");
                    using (Graphics g = Graphics.FromImage(bitmap)) {
                        Rectangle srcRect = new Rectangle(312, 110, 364, 436);
                        Rectangle destRect = new Rectangle(0, 0, 364, 436);
                        g.DrawImage(image, destRect, srcRect, GraphicsUnit.Pixel);
                    }
                    board = CV.ReadBitmap(bitmap);
                }
                State state = new State(board, null);
                state.held = lastHeld;
                State finalState;
                List<Move> moves = FindMoves(state, out finalState);
                if (moves == null) {
                    Thread.Sleep(1000);
                    continue;
                }
                
                foreach (Move move in moves) {
                    Console.WriteLine(move);
                }
                Util32.ExecuteMoves(new Queue<Move>(moves), grabberCol);
                grabberCol = 0;// moves[moves.Count - 1].col;
                lastHeld = finalState.held;
                Thread.Sleep(750);
            }
        }
        
        static List<Move> FindMoves(State state, out State finalState) {
            Console.WriteLine("Searching for a move...");
            Queue<State> queue = new Queue<State>();
            Dictionary<State, Tuple<State, Move>> parents = new Dictionary<State, Tuple<State, Move>>();
            queue.Enqueue(state);
            List<Move> moves = new List<Move>();
            while (queue.Count > 0 && parents.Count < 50000) {
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
                    // Check for match.
                    if (child.Value.HasMatch()) {
                        State node = child.Value;
                        while (parents.ContainsKey(node)) {
                            Tuple<State, Move> parent = parents[node];
                            moves.Add(parent.Item2);
                            node = parent.Item1;
                        }
                        moves.Reverse();
                        finalState = child.Value;
                        return moves;
                    }
                    queue.Enqueue(child.Value);
                }
            }
            finalState = null;
            return null;
        }
    }

    public enum PieceColor { RED, PINK, YELLOW, TEAL, PURPLE, UNKNOWN };
    public class Piece {
        public PieceColor color;
        public bool gem;

        public Piece(PieceColor color, bool gem) {
            this.color = color;
            this.gem = gem;
        }
        public Piece(Piece other) {
            this.color = other.color;
            this.gem = other.gem;
        }
        
        public override string ToString() {
            return color.ToString() + (gem ? "!" : "");
        }
        public string ToString(bool abbrev) {
            return abbrev ? (int)color + (gem ? "!" : "") : ToString();
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

        public State(Piece[,] board, Piece held) {
            this.board = board;
            this.held = held;
        }
        public State(State other) {
            board = (Piece[,])other.board.Clone();
            if (other.held != null) {
                held = new Piece(other.held);
            } else {
                held = null;
            }
        }

        public bool HasMatch() {
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
                        if (board[neighbor.Item1, neighbor.Item2].gem != board[start.Item1, start.Item2].gem) {
                            continue;
                        }
                        count++;
                        if (count >= (board[start.Item1, start.Item2].gem ? 2 : 4)) {
                            return true;
                        }
                        queue.Enqueue(neighbor);
                        toCheck.Remove(neighbor);
                    }
                }
            }
            return false;
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
            return ToString().GetHashCode();
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
            return ToString().GetHashCode();
        }
    }

    public enum Operation { GRAB_OR_DROP, SWAP };
}
