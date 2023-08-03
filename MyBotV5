using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class MyBot : IChessBot
{

    private int[] pieceValues = { 0, 100, 300, 320, 525, 900, 10000 };

    Move bestmoveRoot = Move.NullMove;

    /*
    These are what I call "Layered Bitboards". I use them to alter a piece's value based on its position on the board.
    Normally you would use a 64-int array to store a piece-value chart. However, this uses way too many tokens (64 * 6 = 384 tokens at a minimum)
    This approach uses only 35 tokens to store 6 piece charts with 4 bitboard layers each. More layers = more accurate value chart
    The layered bitboards allow us to increase the value of a square by summing the bitboards. Also, we can use bitwise and to quickly calculate the valuation.
    */
    private ulong[,] pieceCharts =
    {
        //Pawms
        {
            0b0000000011111111111111110011110000011000000110000000000000000000,
            0b0000000011111111111111110001100000011000000000000000000000000000,
            0b0000000011111111001111000000000000000000000000000000000000000000,
            0b0000000011111111000000000000000000000000000000000000000000000000,
            //Black
            0b0000000000000000000110000001100000111100111111111111111100000000,
            0b0000000000000000000000000001100000011000111111111111111100000000,
            0b0000000000000000000000000000000000000000001111001111111100000000,
            0b0000000000000000000000000000000000000000000000001111111100000000,
        },
        //Knights
        {
            //White
            0b0000000001111110011111100111111001111110011111100111111000000000,
            0b0000000000000000001111000011110000111100001111000000000000000000,
            0b0000000000000000000110000011110000111100000110000000000000000000,
            0b0000000000000000000000000001100000011000000000000000000000000000,
            //Black
            0b0000000001111110011111100111111001111110011111100111111000000000,
            0b0000000000000000001111000011110000111100001111000000000000000000,
            0b0000000000000000000110000011110000111100000110000000000000000000,
            0b0000000000000000000000000001100000011000000000000000000000000000,
        },
        //Bishops
        {
            //White
            0b0000000000000000000000000100001000111100010110100101101000000000,
            0b0000000000000000000000000100001000111100010000100100001000000000,
            0b0000000000000000000000000100001000111100000000000100001000000000,
            0b0000000000000000000000000100001000000000000000000100001000000000,
            //Black
            0b0000000001011010010110100011110001000010000000000000000000000000,
            0b0000000001000010010000100011110001000010000000000000000000000000,
            0b0000000001000010000000000011110001000010000000000000000000000000,
            0b0000000001000010000000000000000001000010000000000000000000000000,
        }, 
        //Rooks
        {
            //White
            0b0000000011111111011111100111111001111110011111100111111011111111,
            0b0000000011111111000000000000000000000000000000000000000000111100,
            0b0000000011111111000000000000000000000000000000000000000000011000,
            0b0000000011111111000000000000000000000000000000000000000000000000,
            //Black
            0b1111111101111110011111100111111001111110011111101111111100000000,
            0b0011110000000000000000000000000000000000000000001111111100000000,
            0b0001100000000000000000000000000000000000000000001111111100000000,
            0b0000000000000000000000000000000000000000000000001111111100000000,

        },
        //Queens
        {
            //White
            0b0000000000111100001111000011110001111111010110100111111000000000,
            0b0000000000000000001111000001100000011100000110100000010000000000,
            0b0000000000000000000000000000000000000000000000000000000000000000,
            0b0000000000000000000000000000000000000000000000000000000000000000,
            //Black
            0b0000000001111110010110101111111000111100001111000011110000000000,
            0b0000000000100000010110000011100000011000001111000000000000000000,
            0b0000000000000000000000000000000000000000000000000000000000000000,
            0b0000000000000000000000000000000000000000000000000000000000000000,

        },
        //Kings
        {
            //White
            0b0000000000000000000000000000000000000000000000001100001111100111,
            0b0000000000000000000000000000000000000000000000000000000011000011,
            0b0000000000000000000000000000000000000000000000000000000011000011,
            0b0000000000000000000000000000000000000000000000000000000001000010,
            //Black
            0b1110011111000011000000000000000000000000000000000000000000000000,
            0b1100001100000000000000000000000000000000000000000000000000000000,
            0b1100001100000000000000000000000000000000000000000000000000000000,
            0b0100001000000000000000000000000000000000000000000000000000000000,
        }
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

        bestmoveRoot = Move.NullMove;
        for (int depth = 1; depth <= 50; depth++)
        {
            int score = Search(board, timer, -30000, 30000, depth, 0);

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
        bool notRoot = ply > 0;
        int best = -30000;

        // Check for repetition (this is much more important than material and 50 move rule draws)
        if (notRoot && board.IsRepeatedPosition())
            return 0;

        TTEntry entry = tt[key % entries];

        // TT cutoffs
        if (notRoot && entry.key == key && entry.depth >= depth && (
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
        //The first value in the PieceType class is None
        for (int i = 1; i < pieceValues.Length; i++)
        {
            //First we calculate the difference in base values of all the peices
            ulong whiteBoard = board.GetPieceBitboard((PieceType)i, true);
            ulong blackBoard = board.GetPieceBitboard((PieceType)i, false);
            int whiteCount = BitboardHelper.GetNumberOfSetBits(whiteBoard);
            int blackCount = BitboardHelper.GetNumberOfSetBits(blackBoard);
            eval += (whiteCount - blackCount) * pieceValues[i];

            //Now we account for "bonus" value based on the pieces positions on the board
            //Magic number 4 is the number of bitlayers we have
            for (int j = 0; j < 4; j++)
            {
                ulong whiteBitboardLayer = pieceCharts[i - 1, j]; //Subtracting 1 from the peiceType because our piece charts start at Pawns instead of None
                ulong blackBitboardLayer = pieceCharts[i - 1, j + 4]; //The black layers start right after the white ones hence j+4
                eval += BitboardHelper.GetNumberOfSetBits(whiteBoard & whiteBitboardLayer) * 10;
                eval -= BitboardHelper.GetNumberOfSetBits(blackBoard & blackBitboardLayer) * 10;
            }
        }

        return board.IsWhiteToMove ? eval : -eval;
    }

}
