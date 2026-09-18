using GoSit.Domain.Common;
using Xunit;

namespace GoSit.UnitTests.Domain;

public class EntityTests
{
    private sealed class TestEntity : Entity
    {
        public TestEntity(Guid id) => Id = id;
    }

    private sealed class OtherEntity : Entity
    {
        public OtherEntity(Guid id) => Id = id;
    }

    [Fact]
    public void Entities_with_same_type_and_id_are_equal()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new TestEntity(id), new TestEntity(id));
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        Assert.NotEqual(new TestEntity(Guid.NewGuid()), new TestEntity(Guid.NewGuid()));
    }

    [Fact]
    public void Entities_of_different_types_with_same_id_are_not_equal()
    {
        var id = Guid.NewGuid();

        Assert.False(new TestEntity(id).Equals(new OtherEntity(id)));
    }
}
