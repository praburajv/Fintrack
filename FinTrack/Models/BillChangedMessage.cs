using CommunityToolkit.Mvvm.Messaging.Messages;

namespace FinTracker.Models;

public sealed record BillChanged(DateTime? PreviousDate, DateTime CurrentDate, bool Deleted);

public sealed class BillChangedMessage(BillChanged value) : ValueChangedMessage<BillChanged>(value);
