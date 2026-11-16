namespace Drip.Core.Retrac;

public sealed record RetracSwap(string OwnedId, string OwnedType, string OwnedName, string WantedId, string WantedType, string WantedName, string StyleTag = "", string StyleName = "");
