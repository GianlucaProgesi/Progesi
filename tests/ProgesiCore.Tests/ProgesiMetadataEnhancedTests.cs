using System;
using System.Linq;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using ProgesiCore;

namespace ProgesiCore.Tests
{
    public class ProgesiMetadataEnhancedTests
    {
        [Fact]
        public void Create_WithValidArgs_SetsProperties()
        {
            var refs = new[] { new Uri("http://a"), new Uri("http://b") };
            var m = ProgesiMetadata.Create("ben", additionalInfo: "info", references: refs, id: 1);

            m.Id.Should().Be(1);
            m.CreatedBy.Should().Be("ben");
            m.AdditionalInfo.Should().Be("info");
            m.References.Select(u => u.ToString()).Should().BeEquivalentTo(new[] { "http://a/", "http://b/" });
        }

        [Fact]
        public void Create_WithInvalidId_Throws()
        {
            Action act1 = () => ProgesiMetadata.Create("x", id: 0);
            Action act2 = () => ProgesiMetadata.Create("x", id: -5);
            act1.Should().Throw<ArgumentException>();
            act2.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void UpdateAdditionalInfo_AllowsNullAndTouches()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var before = m.LastModified;
            System.Threading.Thread.Sleep(5);
            m.UpdateAdditionalInfo(null);
            m.AdditionalInfo.Should().BeEmpty();
            m.LastModified.Should().BeAfter(before);
        }

        [Fact]
        public void AddReference_Deduplicates_And_Touches()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var before = m.LastModified;
            m.AddReference(new Uri("http://a"));
            m.AddReference(new Uri("http://a")); // duplicate
            m.AddReference(null);                 // ignored

            m.References.Select(u => u.ToString()).Should().Equal(new[] { "http://a/" });
            m.LastModified.Should().BeOnOrAfter(before);
        }

        [Fact]
        public void AddReferences_SkipsNulls_And_Deduplicates()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var list = new Uri?[] { new Uri("http://a"), null, new Uri("http://a"), new Uri("http://b") };

            m.AddReferences(list!);
            m.References.Select(u => u.ToString()).Should().Equal(new[] { "http://a/", "http://b/" });
        }

        [Fact]
        public void RemoveReference_WhenPresent_Removes_And_Touches()
        {
            var m = ProgesiMetadata.Create("user", id: 1, references: new[] { new Uri("http://a"), new Uri("http://b") });
            var before = m.LastModified;
            System.Threading.Thread.Sleep(5);

            var ok = m.RemoveReference(new Uri("http://a"));
            ok.Should().BeTrue();
            m.References.Select(u => u.ToString()).Should().Equal(new[] { "http://b/" });
            m.LastModified.Should().BeAfter(before);
        }

        [Fact]
        public void RemoveReference_WhenMissingOrNull_ReturnsFalse_NoTouch()
        {
            var m = ProgesiMetadata.Create("user", id: 1, references: new[] { new Uri("http://a") });
            var before = m.LastModified;

            var r1 = m.RemoveReference(new Uri("http://zzz"));
            var r2 = m.RemoveReference(null);

            r1.Should().BeFalse();
            r2.Should().BeFalse();
            m.LastModified.Should().Be(before); // no touch
        }

        [Fact]
        public void RemoveSnip_NotExisting_DoesNotTouch()
        {
            var m = ProgesiMetadata.Create("user", id: 1);
            var before = m.LastModified;
            var r = m.RemoveSnip(Guid.NewGuid());
            r.Should().BeFalse();
            m.LastModified.Should().Be(before);
        }

        [Fact]
        public void Equality_OrderOfReferences_AffectsEquality()
        {
            var a = ProgesiMetadata.Create("u", id: 1, references: new[] { new Uri("http://a"), new Uri("http://b") });
            var b = ProgesiMetadata.Create("u", id: 1, references: new[] { new Uri("http://b"), new Uri("http://a") });

            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void Hash_Changes_When_AdditionalInfo_Changes()
        {
            var a = ProgesiMetadata.Create("user", id: 1, additionalInfo: "alpha");
            var b = ProgesiMetadata.Create("user", id: 1, additionalInfo: "beta");

            ProgesiHash.Compute(a).Should().NotBe(ProgesiHash.Compute(b));
        }
    }
}
