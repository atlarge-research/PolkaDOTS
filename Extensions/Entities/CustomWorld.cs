using System;

namespace Unity.Entities
{
    public unsafe partial class World : IDisposable
    {
        /// <summary>
        /// Numerical ID
        /// </summary>
        public int ID { get; set; }
    }
}