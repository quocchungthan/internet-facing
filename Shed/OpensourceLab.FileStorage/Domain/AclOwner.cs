using OpensourceLab.FileStorage.Meta;

namespace OpensourceLab.FileStorage.Domain
{
    public record AclOwner (
        OwnerType Type,
        string OwnerId // We dont care about this if it's public. let's say an empty Guid toString
    )
    {
        public AclOwner() : this(OwnerType.Public, string.Empty)
        {
        }
    }
}
