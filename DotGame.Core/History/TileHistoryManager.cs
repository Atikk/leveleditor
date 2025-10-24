using System;
using System.Collections.Generic;

namespace DotGame.Core
{
    // Lightweight history manager for tile changes that can be unit tested.
    public sealed class TileHistoryManager
    {
        private readonly Stack<object> undo = new();
        private readonly Stack<object> redo = new();
        private CompositeTileChange? currentComposite;

        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;

        public void BeginComposite()
        {
            if (currentComposite != null)
                throw new InvalidOperationException("Composite already started");
            currentComposite = new CompositeTileChange();
        }

        public CompositeTileChange? EndComposite()
        {
            if (currentComposite == null)
                return null;
            var pushed = currentComposite;
            if (pushed.Changes.Count > 0)
            {
                undo.Push(pushed);
                redo.Clear();
            }
            currentComposite = null;
            return pushed;
        }

        public void Record(TileChange change)
        {
            if (currentComposite != null)
            {
                currentComposite.Changes.Add(change);
                return;
            }

            undo.Push(change);
            redo.Clear();
        }

        public object? PopUndo()
        {
            if (undo.Count == 0)
                return null;
            var e = undo.Pop();
            return e;
        }

        public void PushRedo(object entry)
        {
            redo.Push(entry);
        }

        public object? PopRedo()
        {
            if (redo.Count == 0)
                return null;
            var e = redo.Pop();
            return e;
        }

        public void PushUndo(object entry)
        {
            undo.Push(entry);
        }

        public void Clear()
        {
            undo.Clear();
            redo.Clear();
            currentComposite = null;
        }

        // Small types for use by the manager and tests.
        public sealed class TileChange
        {
            public TileChange(int x, int y, string? oldSerialized, string? newSerialized)
            {
                X = x; Y = y; OldSerialized = oldSerialized; NewSerialized = newSerialized;
            }
            public int X { get; }
            public int Y { get; }
            public string? OldSerialized { get; }
            public string? NewSerialized { get; }
        }

        public sealed class CompositeTileChange
        {
            public CompositeTileChange() { Changes = new List<TileChange>(); }
            public List<TileChange> Changes { get; }
        }
    }
}
