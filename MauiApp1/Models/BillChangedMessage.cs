using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MauiApp1.Models;

public sealed record BillChanged(DateTime? PreviousDate, DateTime CurrentDate, bool Deleted);

public sealed class BillChangedMessage(BillChanged value) : ValueChangedMessage<BillChanged>(value);
