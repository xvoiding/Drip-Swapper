using Drip.Core.Fun;

namespace Drip.App;

public static class FunHub
{
	private static FunStore? _store;

	public static FunStore Store => _store ?? (_store = new FunStore());
}
