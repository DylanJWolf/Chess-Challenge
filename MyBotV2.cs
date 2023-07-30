using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{   

    private Dictionary<ulong, int> visitedPositions = new Dictionary<ulong, int>();

    //To keep track of the values of each piece
    private Dictionary<Enum, int> pieceValues = new Dictionary<Enum, int>
    {
        { PieceType.None, 0 },
        { PieceType.Pawn, 1 },
        { PieceType.Knight, 3 },
        { PieceType.Bishop, 3 },
        { PieceType.Rook, 5 },
        { PieceType.Queen, 9 },
        { PieceType.King, int.MaxValue }
    };

    //Iterates through eaach legal move and evaluates that move using the MiniMax function
    public Move Think(Board board, Timer timer) 
    {
        int depth = 4;
        int bestEval = board.IsWhiteToMove ? int.MinValue : int.MaxValue;
        Move[] moves = board.GetLegalMoves();
        moves = moves.OrderBy(move => move.IsCapture ? 0 : 1).ToArray(); //Puts captures first in the array for better move priority
        Move bestMove = moves[0];

        foreach(Move move in moves)
        {
            board.MakeMove(move);
            int eval = MiniMax(board, depth, int.MinValue, int.MaxValue, board.IsWhiteToMove);
            board.UndoMove(move);

            if (board.IsWhiteToMove)
            {
                if(eval > bestEval)
                {
                    bestEval = eval;
                    bestMove = move;
                }
            }
            else
            {
                if(eval < bestEval)
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
            foreach(Move move in moves)
            {
                board.MakeMove(move);
                int eval = MiniMax(board, depth - 1, alpha, beta,  false);
                board.UndoMove(move); //Must restore the board to its original position before testing the next move
                bestEval = Math.Max(bestEval, eval);
                alpha = Math.Max(alpha, bestEval);
                if(beta <= alpha)
                    break;
            }
            return bestEval;
        }
        else
        {
            int bestEval = int.MaxValue;
            foreach(Move move in moves)
            {
                board.MakeMove(move);
                int eval = MiniMax(board, depth - 1, alpha, beta, true);
                board.UndoMove(move); //Must restore the board to its original position before testing the next move
                bestEval = Math.Min(bestEval, eval);
                beta = Math.Min(beta, bestEval);
                if(beta <= alpha)
                    break;
            }
            return bestEval;
        }
    }

    //Returns an evaluation for the board state. + for white, - for black.
    private int EvaluatePosition(Board board)
    {
        int eval = 0;

        PieceList[] allPieces = board.GetAllPieceLists();
        foreach(PieceList pieces in allPieces)
        {
            int totalValue = pieceValues[pieces.TypeOfPieceInList] * pieces.Count; //Multiply the value of the piece type by the number of pieces in the list
            eval += pieces.IsWhitePieceList ? totalValue : -totalValue; //If the pieces are white, add to the eval. Otherwise, subtract from the eval
        }

        if (board.IsInCheck())
            eval += board.IsWhiteToMove ? -1 : 1; //Subtract 1 from the player in check

        visitedPositions[board.ZobristKey] = eval;
        return eval;
    }
}
