﻿namespace Rhino.DivanDB
{
    public class DatabaseStatistics
    {
        public int CountOfIndexes { get; set; }
        public int CountOfDocuments { get; set; }
        public string[] StaleIndexes { get; set; }
    }
}