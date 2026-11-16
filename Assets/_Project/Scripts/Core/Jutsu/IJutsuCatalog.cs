using System.Collections.Generic;

namespace HandSeals.Core
{
    /// <summary>Source of the techniques available in the game.</summary>
    public interface IJutsuCatalog
    {
        IReadOnlyList<Jutsu> GetAll();
    }
}
