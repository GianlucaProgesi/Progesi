using System;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace ProgesiCore.Tests
{
  public class ProgesiVariableMetadataIdsTests
  {
    [Fact]
    public void MetadataIds_Preserves_Order_And_Removes_Duplicates_FirstWins()
    {
      var v = new ProgesiVariable(1, "A", 1, metadataIds: new[] { 3, 7, 3, 5, 7 });

      v.MetadataIds.Should().Equal(3, 7, 5);
      v.MetadataId.Should().Be(3);
    }

    [Fact]
    public void MetadataIds_Null_Or_Empty_Normalizes_To_Empty_Array()
    {
      var v1 = new ProgesiVariable(1, "A", 1, metadataIds: null);
      var v2 = new ProgesiVariable(2, "B", 2, metadataIds: Array.Empty<int>());

      v1.MetadataIds.Should().BeEmpty();
      v2.MetadataIds.Should().BeEmpty();
      v1.MetadataId.Should().BeNull();
    }

    [Fact]
    public void WithMetadataId_Maps_To_Zero_Or_One_Element_List()
    {
      var empty = new ProgesiVariable(1, "A", 1).WithMetadataId(null);
      var single = new ProgesiVariable(1, "A", 1).WithMetadataId(9);

      empty.MetadataIds.Should().BeEmpty();
      single.MetadataIds.Should().Equal(9);
    }

    [Fact]
    public void WithMetadataIds_Replaces_List()
    {
      var v = new ProgesiVariable(1, "A", 1, metadataIds: new[] { 2 })
        .WithMetadataIds(new[] { 8, 4 });

      v.MetadataIds.Should().Equal(8, 4);
    }

    [Fact]
    public void Equality_Is_Order_Independent_For_MetadataIds()
    {
      var a = new ProgesiVariable(1, "V", 1.0, null, new[] { 3, 7 });
      var b = new ProgesiVariable(1, "V", 1.0, null, new[] { 7, 3 });
      a.Should().Be(b);
      a.GetHashCode().Should().Be(b.GetHashCode());

      var c = new ProgesiVariable(1, "V", 1.0, null, new[] { 3, 8 });
      a.Should().NotBe(c);
    }

    [Fact]
    public void Compute_Hash_Zero_Or_One_Metadata_Is_Byte_Identical_To_Main_8c637ef()
    {
      var noMeta = new ProgesiVariable(11, "Load", 42, new[] { 3, 1, 2 });
      var singleMeta = new ProgesiVariable(11, "Load", 42, new[] { 3, 1, 2 }, metadataIds: new[] { 4 });

      ProgesiHash.Compute(noMeta).Should().Be("1ebd9965c6a742c6077d8ad6767a77f51eb1cf21d6f91ac2915904a758fc6aa7");
      ProgesiHash.Compute(singleMeta).Should().Be("f0f23acbfe86bd3ae84018d5a4e540045d87b7b6be5b657fdab575e658109fa1");
    }

    [Fact]
    public void Compute_Hash_Multi_Metadata_Is_Order_Independent()
    {
      var a = new ProgesiVariable(1, "M", 1, metadataIds: new[] { 7, 3 });
      var b = new ProgesiVariable(1, "M", 1, metadataIds: new[] { 3, 7 });

      ProgesiHash.Compute(a).Should().Be(ProgesiHash.Compute(b));
      ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(new ProgesiVariable(1, "M", 1, metadataIds: new[] { 3 })));
    }
  }
}
