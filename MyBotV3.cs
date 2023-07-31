using ChessChallenge.API;
using System;
using System.Linq;

public class MyBot : IChessBot
{

    private int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

    /*
    These are what I call "Layered Bitboards". I use them to alter a piece's value based on its position on the board.
    Normally you would use a 64-int array to store a piece-value chart. However, this uses way too many tokens (64 * 6 = 384 tokens at a minimum)
    This approach uses only 35 tokens to store 6 piece charts with 4 bitboard layers each. More layers = more accurate value chart
    The layered bitboards allow us to increase the value of a square by summing the bitboards. Also, we can use bitwise and to quickly calculate the valuation.
    */
    private int bitboardLayers = 4;
    private ulong[,] pieceCharts =
    {
        //Pawms
        {
            0b0000000000000000000110000001100000011000000110000000000000000000,
            0b0000000000000000000110000001100000011000000110000000000000000000,
            0b0000000000000000000000000001100000011000000000000000000000000000,
            0b0000000000000000000000000000000000000000000000000000000000000000,
        },
        //Knights
        {
            0b0000000001111110011111100111111001111110011111100111111000000000,
            0b0000000000000000001111000011110000111100001111000000000000000000,
            0b0000000000000000000110000011110000111100000110000000000000000000,
            0b0000000000000000000000000001100000011000000000000000000000000000,
        },
        //Bishops
        {
            0b0000000001000010001111000011110000111100001111000100001000000000,
            0b0000000000000000000000000001100000011000000000000000000000000000,
            0b0000000000000000000000000000000000000000000000000000000000000000,
            0b0000000000000000000000000000000000000000000000000000000000000000,
        }, 
        //Rooks
        {
            0b0011110000111100001111000011110000111100001111000011110000111100,
            0b0011110000111100001111000011110000111100001111000011110000111100,
            0b0001100000011000000110000001100000011000000110000001100000011000,
            0b0001100000011000000110000001100000011000000110000001100000011000,
        },
        //Queens
        {
            0b0000000001111110011111100111111001111110011111100111111000000000,
            0b0000000001111110011111100111111001111110011111100111111000000000,
            0b0000000000000000001111000011110000111100001111000000000000000000,
            0b0000000000000000001111000011110000111100001111000000000000000000,

        },
        //Kings
        {
            0b1111111111000011000000000000000000000000000000001100001111111111,
            0b1110011100000000000000000000000000000000000000000000000011100111,
            0b1100001100000000000000000000000000000000000000000000000011000011,
            0b0100001000000000000000000000000000000000000000000000000001000010,
        },
    };

    //Iterates through eaach legal move and evaluates that move using the MiniMax function
    public Move Think(Board board, Timer timer)
    {
        int depth = 4;
        int bestEval = board.IsWhiteToMove ? int.MinValue : int.MaxValue;
        Move[] moves = board.GetLegalMoves();
        moves = moves.OrderBy(move => move.IsCapture ? 0 : 1).ToArray(); //Puts captures first in the array for better move priority
        Move bestMove = moves[0];

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int eval = MiniMax(board, depth, int.MinValue, int.MaxValue, board.IsWhiteToMove);
            board.UndoMove(move);

            if (board.IsWhiteToMove)
            {
                if (eval > bestEval)
                {
                    bestEval = eval;
                    bestMove = move;
                }
            }
            else
            {
                if (eval < bestEval)
                {
                    bestEval = eval;
                    bestMove = move;
                }
            }
        }

        return bestMove;
    }

    //Evaluates a move based on the resulting board position
    private int MiniMax(Board board, int depth, int alpha, int beta, bool whitePlayer)
    {

        if (board.IsDraw())
            return 0;
        if (depth == 0)
            return EvaluatePosition(board);

        Move[] moves = board.GetLegalMoves(false);
        moves = moves.OrderBy(move => move.IsCapture ? 0 : 1).ToArray();

        if (whitePlayer)
        {
            int bestEval = int.MinValue;
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int eval = MiniMax(board, depth - 1, alpha, beta, false);
                board.UndoMove(move); //Must restore the board to its original position before testing the next move
                bestEval = Math.Max(bestEval, eval);
                alpha = Math.Max(alpha, bestEval);
                if (beta <= alpha)
                    break;
            }
            return bestEval;
        }
        else
        {
            int bestEval = int.MaxValue;
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int eval = MiniMax(board, depth - 1, alpha, beta, true);
                board.UndoMove(move); //Must restore the board to its original position before testing the next move
                bestEval = Math.Min(bestEval, eval);
                beta = Math.Min(beta, bestEval);
                if (beta <= alpha)
                    break;
            }
            return bestEval;
        }
    }

    //Returns an evaluation for the board state. + for white, - for black.
    private int EvaluatePosition(Board board)
    {
        int eval = 0;

        //The first value in the PieceType class is None
        for (int i = 1; i < pieceValues.Length; i++)
        {   
            //First we calculate the difference in base values of all the peices

            PieceType pieceType = (PieceType)i; //Cast int to PieceType
            ulong whiteBoard = board.GetPieceBitboard(pieceType, true);
            ulong blackBoard = board.GetPieceBitboard(pieceType, false); 

            int whiteCount = BitboardHelper.GetNumberOfSetBits(whiteBoard);
            int blackCount = BitboardHelper.GetNumberOfSetBits(blackBoard);
            eval += (whiteCount - blackCount) * pieceValues[i];

            //Now we account for "bonus" value based on the pieces positions on the board
            int bonusVal = 0;
            for(int j = 0; j < bitboardLayers; j++)
            {
                ulong bitboardLayer = pieceCharts[(int)(pieceType) - 1, j]; //Subtracting 1 from the peiceType because our piece charts start at Pawns instead of None
                bonusVal += BitboardHelper.GetNumberOfSetBits(whiteBoard & bitboardLayer);
                bonusVal -= BitboardHelper.GetNumberOfSetBits(blackBoard & bitboardLayer);
            }
            eval += bonusVal * 10;
        }

        if (board.IsInCheck())
            eval += board.IsWhiteToMove ? -1 : 1; //Subtract 1 from the player in check

        return eval; 
    }
}
