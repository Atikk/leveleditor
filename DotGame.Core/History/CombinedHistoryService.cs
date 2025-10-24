using System;
using System.Collections.Generic;

namespace DotGame.Core
{
    // A combined history service that delegates tile history to TileHistoryManager
    // and manages passability history locally. PopUndo/PopRedo return either
    // TileHistoryManager.TileChange/CompositeTileChange or PassabilityChange/CompositePassabilityChange.
    public sealed class CombinedHistoryService
    {
        private readonly TileHistoryManager tileManager = new();
        private readonly Stack<object> passUndo = new();
        private readonly Stack<object> passRedo = new();
        private CompositePassabilityChange? currentPassComposite;

        public int UndoCount => tileManager.UndoCount + passUndo.Count;
        public int RedoCount => tileManager.RedoCount + passRedo.Count;
    public int TileUndoCount => tileManager.UndoCount;
    public int TileRedoCount => tileManager.RedoCount;

        // Tile APIs (thin wrappers)
        public void BeginTileComposite() => tileManager.BeginComposite();
        public TileHistoryManager.CompositeTileChange? EndTileComposite() => tileManager.EndComposite();
        public void RecordTileChange(TileHistoryManager.TileChange change) => tileManager.Record(change);
        public object? PopTileUndo() => tileManager.PopUndo();
        public object? PopTileRedo() => tileManager.PopRedo();
        public void PushTileRedo(object entry) => tileManager.PushRedo(entry);
        public void PushTileUndo(object entry) => tileManager.PushUndo(entry);

        // Passability APIs
        public void BeginPassabilityComposite()
        {
            if (currentPassComposite != null) throw new InvalidOperationException("Passability composite already started");
            currentPassComposite = new CompositePassabilityChange();
        }

        public CompositePassabilityChange? EndPassabilityComposite()
        {
            if (currentPassComposite == null) return null;
            var pushed = currentPassComposite;
            if (pushed.Changes.Count > 0)
            {
                passUndo.Push(pushed);
                passRedo.Clear();
            }
            currentPassComposite = null;
            return pushed;
        }

        public void RecordPassabilityChange(PassabilityChange change)
        {
            if (currentPassComposite != null)
            {
                currentPassComposite.Changes.Add(change);
                return;
            }
            passUndo.Push(change);
            passRedo.Clear();
        }

        // Pop a single entry from the combined undo stack. Preference: tiles were created via tileManager and stored there; we expose combined counts but PopUndo should retrieve the most recent action across both stores.
        // For simplicity we treat tile history and passability history as two independent stacks and prefer tileManager entries if it has any undo entries (this matches previous editor behavior which preferred tile undo when available).
        public object? PopUndo()
        {
            if (tileManager.UndoCount > 0)
                return tileManager.PopUndo();
            if (passUndo.Count > 0)
                return passUndo.Pop();
            return null;
        }

        public void PushRedo(object entry)
        {
            // If entry is a tile entry, push to tile manager redo stack; otherwise to passRedo.
            if (entry is TileHistoryManager.TileChange || entry is TileHistoryManager.CompositeTileChange)
                tileManager.PushRedo(entry);
            else
                passRedo.Push(entry);
        }

        public object? PopRedo()
        {
            if (tileManager.RedoCount > 0)
                return tileManager.PopRedo();
            if (passRedo.Count > 0)
                return passRedo.Pop();
            return null;
        }

        public void PushUndo(object entry)
        {
            if (entry is TileHistoryManager.TileChange || entry is TileHistoryManager.CompositeTileChange)
                tileManager.PushUndo(entry);
            else
                passUndo.Push(entry);
        }

        public void Clear()
        {
            tileManager.Clear();
            passUndo.Clear();
            passRedo.Clear();
            currentPassComposite = null;
        }

        // Passability change types (public so Editor can reference them)
        public sealed class PassabilityChange
        {
            public PassabilityChange(int x, int y, bool oldValue, bool newValue)
            {
                X = x; Y = y; OldValue = oldValue; NewValue = newValue;
            }
            public int X { get; }
            public int Y { get; }
            public bool OldValue { get; }
            public bool NewValue { get; }
        }

        public sealed class CompositePassabilityChange
        {
            public CompositePassabilityChange() { Changes = new List<PassabilityChange>(); }
            public List<PassabilityChange> Changes { get; }
        }
    }
}
