using System.Threading.Tasks;

namespace TokenDock
{
    interface IUsageProvider
    {
        string Id { get; }

        string Name { get; }

        Task<UsageSnapshot> ReadAsync(Settings settings);
    }
}
