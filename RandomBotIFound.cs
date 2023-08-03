using ChessChallenge.API;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    private int[] pieceValues = { 0, 1, 3, 3, 5, 10, 100 };
    private bool mycolor = false;
    private PieceType[] pieces = (PieceType[])Enum.GetValues(typeof(PieceType));

    private Dictionary<ulong, double> memory = new Dictionary<ulong, double>();
    private Dictionary<ulong, int> memoryDepth = new Dictionary<ulong, int>();
    private Random rng = new Random();
    // private const ulong centerBitmask = 66229406270000;

    // https://en.wikipedia.org/wiki/Hamming_weight
    private int BitCounting(ulong input)
    {
        input = input - ((input >> 1) & 0x5555555555555555UL);
        input = (input & 0x3333333333333333UL) + ((input >> 2) & 0x3333333333333333UL);
        return (int)(unchecked(((input + (input >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
    }

    // Update old values with newer depth
    private void updateMemory(ulong b, double score, int depth)
    {
        if (!memory.TryAdd(b, score))
        {
            memory.Remove(b);
            memory.TryAdd(b, score);
        }
        if (!memoryDepth.TryAdd(b, depth))
        {
            memoryDepth.Remove(b);
            memoryDepth.TryAdd(b, depth);
        }
    }

    private double search(ref Board b, ref Timer timer, int depth, double alpha, double beta)
    {
        // Value when Draw (needs to be first to calc repititions)
        if (b.IsDraw())
        {
            return 0;
        }
        // Use Transposition Table
        if (memory.TryGetValue(b.ZobristKey, out double value2) && memoryDepth.TryGetValue(b.ZobristKey, out int d) && d >= depth)
        {
            return value2;
        }
        // Value when Checkmate
        if (b.IsInCheckmate())
        {
            int value = b.IsWhiteToMove != mycolor ? int.MaxValue : int.MinValue;
            updateMemory(b.ZobristKey, value, int.MaxValue);
            return value;
        }
        System.Span<Move> searchMoves = stackalloc Move[256];
        // System.Span<Move> enemyMoves = stackalloc Move[256];
        b.GetLegalMovesNonAlloc(ref searchMoves);
        // if (b.TrySkipTurn())
        // {
        // b.GetLegalMovesNonAlloc(ref enemyMoves);
        // b.UndoSkipTurn();
        // }
        // else
        // {
        // enemyMoves = searchMoves;
        // }

        // bounce if depth or time is over
        //if ((depth < 0) && ((timer.MillisecondsElapsedThisTurn + 100 >= timer.MillisecondsRemaining || timer.MillisecondsElapsedThisTurn > 100) || depth < -500))
        if (depth < 0 || timer.MillisecondsElapsedThisTurn + 100 >= timer.MillisecondsRemaining)
        {
            double value = 0;
            double enemyValue = 0;
            double tm = b.IsWhiteToMove == mycolor ? 1 : -1;
            for (int i = 0; i < pieces.Length; i++)
            {
                value += BitCounting(b.GetPieceBitboard(pieces[i], mycolor)) * pieceValues[(int)pieces[i]];
                // value += BitCounting(b.GetPieceBitboard(pieces[i], mycolor) & centerBitmask) * 0.05;
                enemyValue += BitCounting(b.GetPieceBitboard(pieces[i], !mycolor)) * pieceValues[(int)pieces[i]];
                // enemyValue += BitCounting(b.GetPieceBitboard(pieces[i], !mycolor) & centerBitmask) * 0.05;
            }
            // value += b.IsInCheck() ? 0.3 * -tm : 0; // don't be in check on our turns
            value += searchMoves.Length * tm / 100;
            // enemyValue += enemyMoves.Length * tm / 1000;
            value = (value / enemyValue);// value of an advantage goes up with the fewer peices left
            return value;
        }

        bool maximize = (b.IsWhiteToMove == mycolor);
        double bestVal = maximize ? double.MinValue : double.MaxValue;
        for (int i = 0; i < searchMoves.Length; i++)
        {
            if (searchMoves[i] == Move.NullMove) { continue; }
            b.MakeMove(searchMoves[i]);
            double tvalue = search(ref b, ref timer, depth - 1, alpha, beta);
            b.UndoMove(searchMoves[i]);
            if (maximize)
            {
                bestVal = Math.Max(bestVal, tvalue);
                alpha = Math.Max(alpha, bestVal);
            }
            else
            {
                bestVal = Math.Min(bestVal, tvalue);
                beta = Math.Min(bestVal, beta);
            }
            if (beta <= alpha) { break; }
        }
        updateMemory(b.ZobristKey, bestVal, depth);
        return bestVal;
    }

    public Move Think(Board board, Timer timer)
    {
        int numpieces = (BitCounting(board.AllPiecesBitboard));
        int x = 32 - numpieces;
        int minDepth = 2 + (x * x * x / 6000); //crazy math ik
        mycolor = board.IsWhiteToMove;
        int turn = mycolor ? 1 : -1;
        double best = double.MinValue;

        List<Move> moves = new List<Move>(board.GetLegalMoves());
        moves.RemoveAll(move => move.IsPromotion && (move.PromotionPieceType == PieceType.Rook || move.PromotionPieceType == PieceType.Bishop));
        if (board.PlyCount < 2) { return moves[15]; } // play queen's pawn exlusively
        Move bm = moves[0];

        for (int i = 0; i < moves.Count; i++)
        {
            int r = rng.Next(i, moves.Count);
            Move tmp = moves[r];
            moves[r] = moves[i];
            board.MakeMove(tmp);
            double temp = search(ref board, ref timer, minDepth, double.MinValue, double.MaxValue);
            if (temp > best)
            {
                best = temp;
                bm = tmp;
            }
            board.UndoMove(tmp);
        }
        return bm;
    }
}
