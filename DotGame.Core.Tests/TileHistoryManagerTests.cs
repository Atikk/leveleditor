using System;
using Xunit;
using DotGame.Core;

namespace DotGame.Core.Tests
{
    public class TileHistoryManagerTests
    {
        [Fact]
        public void RecordSingleChange_PushesUndo()
        {
            var mgr = new TileHistoryManager();
            var change = new TileHistoryManager.TileChange(1, 2, "old", "new");
            mgr.Record(change);
            Assert.Equal(1, mgr.UndoCount);
            var popped = mgr.PopUndo();
            Assert.IsType<TileHistoryManager.TileChange>(popped);
            Assert.Equal(0, mgr.UndoCount);
            mgr.PushRedo(popped!);
            Assert.Equal(1, mgr.RedoCount);
        }

        [Fact]
        public void CompositeGrouping_RecordsSingleUndoEntry()
        {
            var mgr = new TileHistoryManager();
            mgr.BeginComposite();
            mgr.Record(new TileHistoryManager.TileChange(0,0, null, "a"));
            mgr.Record(new TileHistoryManager.TileChange(1,1, null, "b"));
            var compositeReturned = mgr.EndComposite();
            Assert.NotNull(compositeReturned);
            Assert.Equal(1, mgr.UndoCount);
            var popped = mgr.PopUndo();
            Assert.IsType<TileHistoryManager.CompositeTileChange>(popped);
            var composite = (TileHistoryManager.CompositeTileChange)popped!;
            Assert.Equal(2, composite.Changes.Count);
        }
    }
}
