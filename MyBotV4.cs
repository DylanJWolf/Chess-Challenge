public class MyBot : IChessBot
{

    private int[] pieceValues = { 0, 100, 300, 320, 525, 900, 10000 };
    private int[] endPieceValues = { 0, 150, 300, 320, 550, 950, 10000}; //The value of the pices change in the end game 
    private const int MAX_DEPTH = 4;

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

    //Iterates through eaach legal move and evaluates that move using the MiniMax function
    public Move Think(Board board, Timer timer)
    {
        int bestEval = board.IsWhiteToMove ? int.MinValue : int.MaxValue;
        Move[] moves = board.GetLegalMoves().OrderByDescending(move => pieceValues[(int)move.CapturePieceType]).ToArray();
        Move bestMove = moves[0];

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            if (board.IsInCheckmate())
                return move;
            int eval = MiniMax(board, MAX_DEPTH, int.MinValue, int.MaxValue, board.IsWhiteToMove);
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

        Move[] moves = board.GetLegalMoves().OrderByDescending(move => pieceValues[(int)move.CapturePieceType]).ToArray();

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
            for (int j = 0; j < bitboardLayers; j++)
            {
                ulong whiteBitboardLayer = pieceCharts[(int)(pieceType) - 1, j]; //Subtracting 1 from the peiceType because our piece charts start at Pawns instead of None
                ulong blackBitboardLayer = pieceCharts[(int)(pieceType) - 1, j + 4]; //The black layers start right after the white ones hence j+4
                bonusVal += BitboardHelper.GetNumberOfSetBits(whiteBoard & whiteBitboardLayer);
                bonusVal -= BitboardHelper.GetNumberOfSetBits(blackBoard & blackBitboardLayer);
            }
            eval += bonusVal * 10;
        }

        return eval;
    }
}
