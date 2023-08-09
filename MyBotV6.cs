using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{

    //The value lookup tables is calculated on startup in the constructor. It holds the value of every piece at every possible position, as calulated from our piece values and piece tables.
    //If any of the piece values are not divisible by 4, they will be truncated by 1 point due to intiger division inside the constructor.
    private int[] pieceValues = { 100, 300, 320, 525, 900, 10000 }, valueLookups = new int[384];
    Move bestmoveRoot = Move.NullMove;

    /*
    These are what I call "Bitlayers". This is my inplementation of piece-square tables.
    Normally you would use a 64-int array to store a PSTB. However, this uses way too many tokens (64 * 6 = 384 tokens at a minimum)
    This approach uses < 30 tokens to store 6 piece charts with 4 bilayers each. More layers = more accurate value chart
    The layered bitboards allow us to increase the value of a square by summing the bitlayers. Also, we can use bitwise and to quickly calculate the valuation.
    */
    private ulong[] pieceTables =
    {
        //Pawms
        0b0000000011111111111111110011110000011000000110000000000000000000,
        0b0000000011111111111111110001100000011000000000000000000000000000,
        0b0000000011111111001111000000000000000000000000000000000000000000,
        0b0000000011111111000000000000000000000000000000000000000000000000,
        //Knights
        0b0000000001111110011111100111111001111110011111100111111000000000,
        0b0000000000000000001111000011110000111100001111000000000000000000,
        0b0000000000000000000110000011110000111100000110000000000000000000,
        0b0000000000000000000000000001100000011000000000000000000000000000,
        //Bishops
        0b0000000000000000000000000100001000111100010110100101101000000000,
        0b0000000000000000000000000100001000111100010000100100001000000000,
        0b0000000000000000000000000100001000111100000000000100001000000000,
        0b0000000000000000000000000100001000000000000000000100001000000000,
        //Rooks
        0b0000000011111111011111100111111001111110011111100111111011111111,
        0b0000000011111111000000000000000000000000000000000000000000111100,
        0b0000000011111111000000000000000000000000000000000000000000011000,
        0b0000000011111111000000000000000000000000000000000000000000000000,
        //Queen
        0b0000000000111100001111000011110001111111010110100111111000000000,
        0b0000000000000000001111000001100000011100000110100000010000000000,
        0b0000000000000000000000000000000000000000000000000000000000000000,
        0b0000000000000000000000000000000000000000000000000000000000000000,
        //Kings
        0b0000000000000000000000000000000000000000000000001100001111100111,
        0b0000000000000000000000000000000000000000000000000000000011000011,
        0b0000000000000000000000000000000000000000000000000000000011000011,
        0b0000000000000000000000000000000000000000000000000000000001000010,
    };


    struct TTEntry
    {
        public ulong key;
        public Move move;
        public int depth, score, bound;
        public TTEntry(ulong _key, Move _move, int _depth, int _score, int _bound)
        {
            key = _key; move = _move; depth = _depth; score = _score; bound = _bound;
        }
    }

    const int entries = (1 << 20);
    TTEntry[] tt = new TTEntry[entries];

    //Iterates through eaach legal move and evaluates that move using the MiniMax function
    public Move Think(Board board, Timer timer)
    {
        bestmoveRoot = Move.NullMove; //Yes this is neccissary! We need to return a move if we dont have time to finish a depth of 1, and we dont want to calculate getLegalMoves unless we need to

        for (int depth = 1; depth <= 50; depth++)
        {
            Search(board, timer, -30000, 30000, depth, 0);

            // Out of time
            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30)
                break;
        }
        return bestmoveRoot.IsNull ? board.GetLegalMoves()[0] : bestmoveRoot; 
    }

    //Evaluates a move based on the resulting board position
    public int Search(Board board, Timer timer, int alpha, int beta, int depth, int ply)
    {
        ulong key = board.ZobristKey;
        bool qsearch = depth <= 0;
        int best = -30000;

        // Check for repetition (this is much more important than material and 50 move rule draws)
        if (ply > 0 && board.IsRepeatedPosition())
            return 0;

        TTEntry entry = tt[key % entries];

        // TT cutoffs
        if (ply > 0 && entry.key == key && entry.depth >= depth && (
            entry.bound == 3 // exact score
                || entry.bound == 2 && entry.score >= beta // lower bound, fail high
                || entry.bound == 1 && entry.score <= alpha // upper bound, fail low
        )) return entry.score;

        int eval = EvaluatePosition(board);

        // Quiescence search is in the same function as negamax to save tokens
        if (qsearch)
        {
            best = eval;
            if (best >= beta) return best;
            alpha = Math.Max(alpha, best);
        }

        // Generate moves, only captures in qsearch
        Move[] moves = board.GetLegalMoves(qsearch);
        int[] scores = new int[moves.Length];

        // Score moves
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            // TT move
            if (move == entry.move) scores[i] = 1000000;
            // https://www.chessprogramming.org/MVV-LVA
            else if (move.IsCapture) scores[i] = 100 * (int)move.CapturePieceType - (int)move.MovePieceType;
        }

        Move bestMove = Move.NullMove;
        int origAlpha = alpha;

        // Search moves
        for (int i = 0; i < moves.Length; i++)
        {
            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30) return 30000;

            // Incrementally sort moves
            for (int j = i + 1; j < moves.Length; j++)
            {
                if (scores[j] > scores[i])
                    (scores[i], scores[j], moves[i], moves[j]) = (scores[j], scores[i], moves[j], moves[i]);
            }

            Move move = moves[i];

            board.MakeMove(move);
            int score = -Search(board, timer, -beta, -alpha, depth - 1, ply + 1);
            board.UndoMove(move);

            if (score > best)
            {
                best = score;
                bestMove = move;
                if (ply == 0) bestmoveRoot = move;
                alpha = Math.Max(alpha, score);
                if (alpha >= beta) break;
            }
        }

        // (Check/Stale)mate
        if (!qsearch && moves.Length == 0) return board.IsInCheck() ? -30000 + ply : 0;

        // Did we fail high/low or get an exact score?
        int bound = best >= beta ? 2 : best > origAlpha ? 3 : 1;

        // Push to TT
        tt[key % entries] = new TTEntry(key, bestMove, depth, best, bound);

        return best;
    }

    //Returns an evaluation for the board state. + for white, - for black.
    private int EvaluatePosition(Board board)
    {
        int eval = 0;

        for(int i = 0; i < 6; i++)
        {
            ulong whiteBoard = board.GetPieceBitboard((PieceType)i + 1, true), blackBoard = board.GetPieceBitboard((PieceType)i + 1, false);

            //Perhaps we could do something like while(whiteBoard | blackBoard > 0) ? and expand valueIndexes with the last values being 0, so adding an index that is out of bounds will not matter
            while (whiteBoard > 0)
                eval += valueLookups[i * 64 + BitboardHelper.ClearAndGetIndexOfLSB(ref whiteBoard)];
            while (blackBoard > 0)
                eval -= valueLookups[i * 64 + 63 - BitboardHelper.ClearAndGetIndexOfLSB(ref blackBoard)];
        }

        return board.IsWhiteToMove ? eval : -eval;
    }


    //Pre-generates piece-value lookup tables for every possible piece position.
    public MyBot()
    {
        //Yeah we could possibly make this even smaller by removing the inner loop and multiplying the outer bounds by the number of bitlayers.
        for (int i = 0; i < 384; i++)
            for (int bitLayerIndex = 0; bitLayerIndex < 4; bitLayerIndex++)
                valueLookups[i] += pieceValues[i / 64] / 4 + ((pieceTables[i / 64 * 4 + bitLayerIndex] & 1ul << i % 64) > 0 ? 10 : 0); //This is the most unreadable line of code ever. But hey, token optimization.

        /*for (int i = 0; i < valueIndexes.Length; i++)
        {
            PieceType pieceType = (PieceType)(int)(i / 64) + 1;
            int index = i % 64;
            int flippedIndex = 63 - index;
            int whiteVal = valueIndexes[i];
            int blackVal = valueIndexes[i - index + flippedIndex];
            Console.WriteLine(pieceType.ToString() + " Square: " + index.ToString() + " White Value: " + whiteVal.ToString() + " Black Value: " + blackVal.ToString());
        }*/
    }
}
