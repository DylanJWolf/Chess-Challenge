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
        //Kings endgame
        0b0000000001111110011111100111111001111110011111100111111000000000,
        0b0000000000000000001111000011110000111100001111000000000000000000,
        0b0000000000000000001111000011110000111100001111000000000000000000,
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

        //This is needed. Otherwise, the bot does not know that checkmate is a good thing. Also, we subract ply from the score so checkmates that a mate in 1 is better than a mate in 2 and so on.
        if (board.IsInCheckmate())
            return best - ply;

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
                if (scores[j] > scores[i])
                    (scores[i], scores[j], moves[i], moves[j]) = (scores[j], scores[i], moves[j], moves[i]);

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
        int eval = 0, max = BitboardHelper.GetNumberOfSetBits(board.AllPiecesBitboard) > 14 ? 6 : 5; //Crude endgame detection

        for (int c = 0; c < 2; c++)
        {
            bool isWhite = c == 0;

            //If this is the endgame, we calculate king values seperately
            //Maybe this isn't entirely needed. What if we just ignore kings alltogether in the endgame? Obviously that is worse, but maybe its worth the token savings
            //Or reduce the eg king table to just one bitlayer? would eliminate the loop.
            if (max < 6)
                for (int k = 0; k < 3; k++)  //Only 3 layers for the endgame kings ATM.
                    eval += (board.GetPieceBitboard(PieceType.King, isWhite) & pieceTables[24 + k]) > 0 ? 10 : 0; //It might be faster to define the king outside of the loop. Luckily, the king's EG PSTB is symetrical, no need to flip anything.

            //Sum up piece values 
            for (int i = max; i-- > 0;)
            {
                ulong pieceBoard = board.GetPieceBitboard((PieceType)i + 1, isWhite);

                while (pieceBoard > 0)
                {
                    int index = BitboardHelper.ClearAndGetIndexOfLSB(ref pieceBoard);
                    eval += valueLookups[i * 64 + (isWhite ? index : 63 - index)];
                }
            }

            eval = -eval;
        }

        return board.IsWhiteToMove ? eval : -eval;
    }


    //Pre-generates piece-value lookup tables for every possible piece position.
    public MyBot()
    {
        for (int i = 0; i < 1536; i++)
        {
            int valueIndex = i / 4;
            valueLookups[valueIndex] += pieceValues[valueIndex / 64] / 4 + ((pieceTables[valueIndex / 64 * 4 + i % 4] & 1ul << valueIndex % 64) > 0 ? 10 : 0);
        }
    }
}
