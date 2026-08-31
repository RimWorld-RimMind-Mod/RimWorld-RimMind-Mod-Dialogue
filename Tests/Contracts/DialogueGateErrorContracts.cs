using System.Collections.Concurrent;
using System.Threading.Tasks;
using RimMind.Dialogue.Core;
using RimMind.Testing;
using Xunit;

namespace RimMind.Dialogue.Tests.Contracts
{
    public sealed class DialogueGateErrorContracts
    {
        [Fact]
        public void Gate_error_and_stale_response_boundaries()
        {
            ContractCaseRunner.Run(
                ("pawn and pair reservations are acquired atomically", AtomicPawnAndPairReservation),
                ("concurrent contenders cannot exceed global capacity", ConcurrentContendersRespectCapacity),
                ("global request capacity is enforced by the same reservation boundary", GlobalCapacityIsAtomic),
                ("request errors release pawn and pair reservations", DisposalReleasesReservations),
                ("cleanup is idempotent across overlapping error paths", CleanupIsIdempotent),
                ("lifecycle reset fences stale lease cleanup", ResetFencesStaleLeaseCleanup),
                ("active recipient cleanup is request ownership aware", RecipientCleanupIsOwnershipAware));
        }

        private static void AtomicPawnAndPairReservation()
        {
            var reservations = new DialogueRequestReservations();
            Assert.True(reservations.TryAcquire(1, (1, 2), 4, out var first));
            Assert.False(reservations.TryAcquire(1, (1, 3), 4, out _));
            Assert.False(reservations.TryAcquire(3, (1, 2), 4, out _));
            Assert.True(reservations.IsPawnPending(1));
            Assert.True(reservations.IsPairPending((1, 2)));
            first!.Dispose();
        }

        private static void GlobalCapacityIsAtomic()
        {
            var reservations = new DialogueRequestReservations();
            Assert.True(reservations.TryAcquire(1, null, 1, out var first));
            Assert.False(reservations.TryAcquire(2, null, 1, out _));
            Assert.Equal(1, reservations.ActivePawnCount);
            first!.Dispose();
        }

        private static void ConcurrentContendersRespectCapacity()
        {
            var reservations = new DialogueRequestReservations();
            var leases = new ConcurrentBag<DialogueRequestReservations.DialogueReservation>();
            Parallel.For(0, 32, pawnId =>
            {
                if (reservations.TryAcquire(pawnId, null, 1, out var lease))
                    leases.Add(lease!);
            });

            Assert.Single(leases);
            Assert.Equal(1, reservations.ActivePawnCount);
            foreach (var lease in leases)
                lease.Dispose();
        }

        private static void DisposalReleasesReservations()
        {
            var reservations = new DialogueRequestReservations();
            Assert.True(reservations.TryAcquire(7, (7, 8), 2, out var lease));
            lease!.Dispose();

            Assert.False(reservations.IsPawnPending(7));
            Assert.False(reservations.IsPairPending((7, 8)));
            Assert.True(reservations.TryAcquire(8, (7, 8), 2, out var next));
            next!.Dispose();
        }

        private static void CleanupIsIdempotent()
        {
            var reservations = new DialogueRequestReservations();
            Assert.True(reservations.TryAcquire(9, null, 1, out var lease));
            lease!.Dispose();
            lease.Dispose();
            Assert.Equal(0, reservations.ActivePawnCount);
        }

        private static void ResetFencesStaleLeaseCleanup()
        {
            var reservations = new DialogueRequestReservations();
            Assert.True(reservations.TryAcquire(1, (1, 2), 1, out var stale));
            reservations.Reset();
            Assert.True(reservations.TryAcquire(1, (1, 2), 1, out var current));

            stale!.Dispose();
            Assert.True(reservations.IsPawnPending(1));
            Assert.True(reservations.IsPairPending((1, 2)));
            current!.Dispose();
        }

        private static void RecipientCleanupIsOwnershipAware()
        {
            var recipients = new DialogueActiveRecipientRegistry();
            recipients.SetRequest(pawnId: 1, recipientId: 2, ownerId: 10);
            recipients.SetRequest(pawnId: 1, recipientId: 3, ownerId: 11);

            Assert.False(recipients.ClearRequestIfOwned(pawnId: 1, ownerId: 10));
            Assert.True(recipients.TryGetRecipient(1, out int currentRecipient));
            Assert.Equal(3, currentRecipient);
            recipients.SetManual(pawnId: 1, recipientId: 4);
            Assert.True(recipients.TryGetRecipient(1, out currentRecipient));
            Assert.Equal(4, currentRecipient);
            recipients.ClearManual(1);
            Assert.True(recipients.TryGetRecipient(1, out currentRecipient));
            Assert.Equal(3, currentRecipient);
            Assert.True(recipients.ClearRequestIfOwned(pawnId: 1, ownerId: 11));
            Assert.False(recipients.TryGetRecipient(1, out _));
        }
    }
}
