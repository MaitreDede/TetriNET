﻿using System.Collections.Generic;
using TetriNET.Common.Attributes;
using TetriNET.Common.DataContracts;
using TetriNET.Common.Helpers;
using TetriNET.Common.Interfaces;

namespace TetriNET.Client
{
    internal sealed class Statistics : IClientStatistics
    {
        public Dictionary<Pieces, int> PieceCount { get; set; }
        public Dictionary<Specials, int> SpecialCount { get; set; }
        public Dictionary<Specials, int> SpecialUsed { get; set; }
        public Dictionary<Specials, int> SpecialDiscarded { get; set; }

        public int EndOfPieceQueueReached { get; set; }
        public int NextPieceNotYetReceived { get; set; }

        public Statistics()
        {
            PieceCount = new Dictionary<Pieces, int>();
            SpecialCount = new Dictionary<Specials, int>();
            SpecialUsed = new Dictionary<Specials, int>();
            SpecialDiscarded = new Dictionary<Specials, int>();

            foreach (Pieces piece in EnumHelper.GetPieces(availabilities => availabilities == Availabilities.Available))
                PieceCount.Add(piece, 0);
            foreach (Specials special in EnumHelper.GetSpecials(available => available))
            {
                SpecialCount.Add(special, 0);
                SpecialUsed.Add(special, 0);
                SpecialDiscarded.Add(special, 0);
            }
        }

        public void Reset()
        {
            foreach (Pieces piece in EnumHelper.GetPieces(availabilities => availabilities == Availabilities.Available))
                PieceCount[piece] = 0;
            foreach (Specials special in EnumHelper.GetSpecials(available => available))
            {
                SpecialCount[special] = 0;
                SpecialUsed[special] = 0;
                SpecialDiscarded[special] = 0;
            }
            EndOfPieceQueueReached = 0;
            NextPieceNotYetReceived = 0;
        }
    }
}