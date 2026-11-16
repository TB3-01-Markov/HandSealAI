namespace HandSeals.Core
{
    /// <summary>Loads and saves <see cref="PlayerProgress"/>.</summary>
    public interface IProgressRepository
    {
        PlayerProgress Load();

        void Save(PlayerProgress progress);
    }
}
