using System;
using Drip.Core.RocketLeague.Custom;

namespace Drip.Core.FortniteModels;

public sealed record BuildLook(string Id, string Name, string Blurb, Func<int, TexImage> Make);
