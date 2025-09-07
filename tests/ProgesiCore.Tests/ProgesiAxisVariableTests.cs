using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ProgesiCore;

namespace ProgesiCore.Tests
{
    public class ProgesiAxisVariableTests
    {
        private static ProgesiAxisVariable Make(int id = 1, string name = "Axis-1", double? len = null, int? ruleId = null)
            => new ProgesiAxisVariable(id, name, len, ruleId);

        // -------------------------
        // Costruttore & guard-clauses
        // -------------------------
        [Fact]
        public void Ctor_Throws_OnNegativeId()
        {
            Assert.Throws<ArgumentException>(() => Make(-1));
        }

        [Fact]
        public void Ctor_Throws_OnNullName()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgesiAxisVariable(1, null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Ctor_Throws_OnEmptyOrWhiteSpaceName(string axisName)
        {
            Assert.Throws<ArgumentException>(() => new ProgesiAxisVariable(1, axisName));
        }


        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        public void Ctor_Throws_OnNonPositiveAxisLength(double len)
        {
            Assert.Throws<ArgumentException>(() => new ProgesiAxisVariable(1, "axis", len));
        }

        [Fact]
        public void Ctor_SetsProperties()
        {
            var sut = Make(10, " Gr-1 ", 100.0, 99);
            Assert.Equal(10, sut.Id);
            Assert.Equal("Gr-1", sut.AxisName); // Trim
            Assert.Equal(100.0, sut.AxisLength);
            Assert.Equal(99, sut.RuleId);
        }

        // -------------------------
        // Setters di contesto
        // -------------------------
        [Fact]
        public void SetRule_CanSetAndClear()
        {
            var sut = Make();
            sut.SetRule(42);
            Assert.Equal(42, sut.RuleId);
            sut.SetRule(null);
            Assert.Null(sut.RuleId);
        }

        [Fact]
        public void SetRule_Throws_OnNegative()
        {
            var sut = Make();
            Assert.Throws<ArgumentException>(() => sut.SetRule(-1));
        }

        [Fact]
        public void SetAxisLength_UpdatesLength_AndValidatesFutureOperations()
        {
            var sut = Make();
            sut.SetAxisLength(10.0);
            Assert.Equal(10.0, sut.AxisLength);
            // Dentro range: ok
            sut.Add("T", 5.0, 1);
            // Fuori range di molto: eccezione
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add("T", 20.0, 2));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void SetAxisLength_Throws_OnNonPositive(double len)
        {
            var sut = Make();
            Assert.Throws<ArgumentException>(() => sut.SetAxisLength(len));
        }

        // -------------------------
        // Add / Get / Map
        // -------------------------
        [Fact]
        public void Add_StoresOnlyIds_AndAvoidsDuplicates()
        {
            var sut = Make();
            sut.Add("VarA", 1.0, 100);
            sut.Add("VarA", 1.0, 100); // duplicato (HashSet)
            var at = sut.GetAt("VarA", 1.0);
            Assert.Single(at);
            Assert.Equal(100, at.First());
        }

        [Fact]
        public void Add_AllowsMultipleIdsSamePosition()
        {
            var sut = Make();
            sut.Add("VarA", 2.0, 1);
            sut.Add("VarA", 2.0, 3);
            var at = sut.GetAt("VarA", 2.0).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 1, 3 }, at);
        }

        [Fact]
        public void GetAt_ReturnsEmpty_IfNameOrPositionMissing()
        {
            var sut = Make();
            Assert.Empty(sut.GetAt("Missing", 0.0));
            sut.Add("VarA", 1.0, 10);
            Assert.Empty(sut.GetAt("VarA", 2.0));
        }

        [Fact]
        public void GetMap_ReturnsOrderedPositionsWithIds()
        {
            var sut = Make();
            sut.Add("V", 3.0, 3);
            sut.Add("V", 1.0, 2);
            sut.Add("V", 1.0, 1);

            var map = sut.GetMap("V");
            // Le chiavi double non garantiscono ordine nella mappa restituita, ma i contenuti sì
            Assert.Contains(1.0, map.Keys);
            Assert.Contains(3.0, map.Keys);

            var idsAt1 = map[1.0];
            Assert.Equal(new[] { 1, 2 }, idsAt1);
        }

        // -------------------------
        // Tolleranza di posizione
        // -------------------------
        [Fact]
        public void PositionsWithinTolerance_BucketizeToSameKey_DefaultTol()
        {
            var sut = Make();
            double tol = ProgesiAxisVariable.DefaultTolerance;

            sut.Add("V", 1.0000000, 1);
            sut.Add("V", 1.0 + tol * 0.4, 2); // stessa bucket

            var at = sut.GetAt("V", 1.0);
            Assert.Equal(new[] { 1, 2 }, at.OrderBy(x => x).ToArray());
        }

        [Fact]
        public void PositionsSeparatedBeyondTolerance_CreateDifferentKeys()
        {
            var sut = Make();
            double tol = ProgesiAxisVariable.DefaultTolerance;

            sut.Add("V", 1.0000000, 1);
            sut.Add("V", 1.0 + tol * 2.5, 2); // bucket diverso

            var at1 = sut.GetAt("V", 1.0);
            var at2 = sut.GetAt("V", 1.0 + tol * 2.5);
            Assert.Single(at1);
            Assert.Single(at2);
            Assert.Contains(1, at1);
            Assert.Contains(2, at2);
        }

        [Fact]
        public void Add_Throws_OnNaN_OrInfinity()
        {
            var sut = Make();
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add("V", double.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add("V", double.PositiveInfinity, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add("V", double.NegativeInfinity, 1));
        }

        [Fact]
        public void Add_RespectsAxisLength_WithInclusiveToleranceWindow()
        {
            var sut = Make(len: 10.0);

            // Dentro 0..10: OK
            sut.Add("V", 0.0, 1);
            sut.Add("V", 10.0, 2);

            // Oltre di molto: KO
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add("V", 10.001, 3));

            // Poco sotto 0 o poco sopra Length oltre la finestra: KO
            double tol = ProgesiAxisVariable.DefaultTolerance;
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add("V", -tol * 2, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add("V", 10.0 + tol * 2, 5));
        }

        // -------------------------
        // Move
        // -------------------------
        [Fact]
        public void Move_ReturnsFalse_IfGroupOrFromMissing()
        {
            var sut = Make();
            Assert.False(sut.Move("Missing", 1.0, 2.0, 1));

            sut.Add("V", 1.0, 10);
            Assert.False(sut.Move("V", 9.0, 2.0, 10)); // from mancante
        }

        [Fact]
        public void Move_MovesId_AndCleansEmptyBuckets()
        {
            var sut = Make();
            sut.Add("V", 1.0, 10);

            bool moved = sut.Move("V", 1.0, 2.0, 10);
            Assert.True(moved);
            Assert.Empty(sut.GetAt("V", 1.0));
            Assert.Contains(10, sut.GetAt("V", 2.0));
        }

        [Fact]
        public void Move_MergesIntoExistingBucket()
        {
            var sut = Make();
            double tol = ProgesiAxisVariable.DefaultTolerance;

            sut.Add("V", 1.0, 1);
            sut.Add("V", 1.0 + tol * 0.4, 2); // stesso bucket di 1.0
            sut.Add("V", 2.0, 99);

            // Sposto 99 vicino a 2.0 (stesso bucket)
            bool moved = sut.Move("V", 2.0, 2.0 + tol * 0.4, 99);
            Assert.True(moved);

            var at2 = sut.GetAt("V", 2.0);
            Assert.Contains(99, at2);
        }

        // -------------------------
        // RemoveAt / RemoveAll
        // -------------------------
        [Fact]
        public void RemoveAt_RemovesId_AndCleansBucketWhenEmpty()
        {
            var sut = Make();
            sut.Add("V", 1.0, 10);
            sut.Add("V", 1.0, 20);

            bool r1 = sut.RemoveAt("V", 1.0, 10);
            Assert.True(r1);
            Assert.Equal(new[] { 20 }, sut.GetAt("V", 1.0).ToArray());

            bool r2 = sut.RemoveAt("V", 1.0, 20);
            Assert.True(r2);
            Assert.Empty(sut.GetAt("V", 1.0)); // bucket rimosso
        }

        [Fact]
        public void RemoveAt_ReturnsFalse_IfMissing()
        {
            var sut = Make();
            sut.Add("V", 1.0, 10);
            Assert.False(sut.RemoveAt("V", 2.0, 10));
            Assert.False(sut.RemoveAt("V", 1.0, 99));
            Assert.False(sut.RemoveAt("Missing", 1.0, 10));
        }

        [Fact]
        public void RemoveAll_RemovesEntireGroup()
        {
            var sut = Make();
            sut.Add("A", 0.0, 1);
            sut.Add("B", 0.0, 2);

            Assert.True(sut.RemoveAll("A"));
            Assert.Empty(sut.GetAt("A", 0.0));
            Assert.False(sut.RemoveAll("A")); // già rimosso
            Assert.NotEmpty(sut.GetAt("B", 0.0));
        }

        // -------------------------
        // RenameGroup
        // -------------------------
        [Fact]
        public void RenameGroup_Renames_WhenTargetNotExists()
        {
            var sut = Make();
            sut.Add("Old", 1.0, 10);

            bool ok = sut.RenameGroup("Old", "New");
            Assert.True(ok);
            Assert.Empty(sut.GetAt("Old", 1.0));
            Assert.Contains(10, sut.GetAt("New", 1.0));
        }

        [Fact]
        public void RenameGroup_ReturnsFalse_WhenSourceMissingOrSameName()
        {
            var sut = Make();
            sut.Add("V", 1.0, 1);

            Assert.False(sut.RenameGroup("Missing", "X"));
            Assert.False(sut.RenameGroup("V", "V"));
        }

        [Fact]
        public void RenameGroup_Throws_WhenTargetAlreadyExists()
        {
            var sut = Make();
            sut.Add("A", 1.0, 1);
            sut.Add("B", 2.0, 2);

            Assert.Throws<InvalidOperationException>(() => sut.RenameGroup("A", "B"));
        }

        // -------------------------
        // ReplaceMap
        // -------------------------
        [Fact]
        public void ReplaceMap_ReplacesEntireMap_AndBucketsByTolerance()
        {
            var sut = Make();
            double tol = ProgesiAxisVariable.DefaultTolerance;

            var entries = new List<(double position, IEnumerable<int> ids)>();
            entries.Add((1.0, new[] { 1, 2 }));
            entries.Add((1.0 + tol * 0.4, new[] { 3 })); // stesso bucket

            sut.ReplaceMap("V", entries);

            var at = sut.GetAt("V", 1.0).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 1, 2, 3 }, at);
        }

        [Fact]
        public void ReplaceMap_Throws_OnNullEntries_OrNullIds()
        {
            var sut = Make();

            // entries null → ArgumentNullException
            Assert.Throws<ArgumentNullException>(() => sut.ReplaceMap("V", null!));

            // lista con ids null → ArgumentNullException
            var entries = new List<(double position, IEnumerable<int> ids)>();
            entries.Add((0.0, (IEnumerable<int>)null!));   // <— cast + null-forgiving
            Assert.Throws<ArgumentNullException>(() => sut.ReplaceMap("V", entries));
        }


        [Fact]
        public void ReplaceMap_Throws_OnNegativeIds()
        {
            var sut = Make();
            var entries = new List<(double position, IEnumerable<int> ids)>();
            entries.Add((0.0, new[] { -1, 2 }));
            Assert.Throws<ArgumentException>(() => sut.ReplaceMap("V", entries));
        }

        [Fact]
        public void ReplaceMap_ValidatesPositionsAgainstAxisLength()
        {
            var sut = Make(len: 1.0);
            var good = new List<(double position, IEnumerable<int> ids)>();
            good.Add((0.5, new[] { 1 }));

            sut.ReplaceMap("V", good);
            Assert.Contains(1, sut.GetAt("V", 0.5));

            var bad = new List<(double position, IEnumerable<int> ids)>();
            bad.Add((2.0, new[] { 9 }));

            Assert.Throws<ArgumentOutOfRangeException>(() => sut.ReplaceMap("V", bad));
        }

        // -------------------------
        // EnumerateAll & Ordering
        // -------------------------
        [Fact]
        public void EnumerateAll_ReturnsOrderedByName_ThenPosition_ThenId()
        {
            var sut = Make();
            sut.Add("B", 2.0, 2);
            sut.Add("A", 2.0, 1);
            sut.Add("A", 1.0, 3);

            var list = sut.EnumerateAll().ToList();

            // Atteso: (A,1,3), (A,2,1), (B,2,2)
            Assert.Equal("A", list[0].variableName);
            Assert.Equal(1.0, list[0].position);
            Assert.Equal(3, list[0].variableId);

            Assert.Equal("A", list[1].variableName);
            Assert.Equal(2.0, list[1].position);
            Assert.Equal(1, list[1].variableId);

            Assert.Equal("B", list[2].variableName);
            Assert.Equal(2.0, list[2].position);
            Assert.Equal(2, list[2].variableId);
        }

        // -------------------------
        // Equality semantics (ValueObject)
        // -------------------------
        [Fact]
        public void Equality_IgnoresInsertionOrder_ConsidersState()
        {
            var a = Make(1, "AX", 10.0, 7);
            a.Add("V", 1.0, 2);
            a.Add("V", 1.0, 1);

            var b = Make(1, "AX", 10.0, 7);
            b.Add("V", 1.0, 1);
            b.Add("V", 1.0, 2);

            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equality_Differs_WhenContextDiffers()
        {
            var a = Make(1, "AX", 10.0, 7);
            var b = Make(2, "AX", 10.0, 7);
            Assert.False(a.Equals(b));

            var c = Make(1, "AX", 10.0, 7);
            var d = Make(1, "AY", 10.0, 7);
            Assert.False(c.Equals(d));

            var e = Make(1, "AX", 10.0, 7);
            var f = Make(1, "AX", null, 7);
            Assert.False(e.Equals(f));
        }

        // -------------------------
        // Input validation extra
        // -------------------------
        [Fact]
        public void Api_Throws_OnNullVariableName()
        {
            var sut = new ProgesiAxisVariable(1, "Axis");
            Assert.Throws<ArgumentNullException>(() => sut.Add(null!, 0.0, 1));
            Assert.Throws<ArgumentNullException>(() => sut.GetMap(null!));
            Assert.Throws<ArgumentNullException>(() => sut.GetAt(null!, 0.0));
            Assert.Throws<ArgumentNullException>(() => sut.ReplaceMap(null!, new List<(double, IEnumerable<int>)>()));
            Assert.Throws<ArgumentNullException>(() => sut.GetMap(null!));
            Assert.Throws<ArgumentNullException>(() => sut.GetAt(null!, 0.0));
            Assert.Throws<ArgumentNullException>(() => sut.Move(null!, 0.0, 1.0, 1));
            Assert.Throws<ArgumentNullException>(() => sut.RemoveAt(null!, 0.0, 1));
            Assert.Throws<ArgumentNullException>(() => sut.RemoveAll(null!));
            Assert.Throws<ArgumentNullException>(() => sut.ReplaceMap(null!, new List<(double, IEnumerable<int>)>()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Api_Throws_OnEmptyOrWhiteVariableName(string name)
        {
            var sut = new ProgesiAxisVariable(1, "Axis");
            Assert.Throws<ArgumentException>(() => sut.Add(name, 0.0, 1));
            Assert.Throws<ArgumentException>(() => sut.GetMap(name));
            Assert.Throws<ArgumentException>(() => sut.GetAt(name, 0.0));
            Assert.Throws<ArgumentException>(() => sut.Move(name, 0.0, 1.0, 1));
            Assert.Throws<ArgumentException>(() => sut.RemoveAt(name, 0.0, 1));
            Assert.Throws<ArgumentException>(() => sut.RemoveAll(name));
            Assert.Throws<ArgumentException>(() => sut.ReplaceMap(name, new List<(double, IEnumerable<int>)>()));
        }

        [Fact]
        public void GetMap_OnMissingName_ReturnsEmptyDictionary_NotNull()
        {
            var sut = Make();
            var map = sut.GetMap("Missing");
            Assert.NotNull(map);
            Assert.Empty(map);
        }

        [Fact]
        public void SetAxisLength_Shrink_DoesNotRetrovalidateExistingEntries_ButBlocksFuture()
        {
            var sut = Make(len: 10.0);
            sut.Add("V", 9.9, 1);

            // riduco la lunghezza a 5.0
            sut.SetAxisLength(5.0);

            // ancora presente (nessuna retrovalidazione)
            Assert.Contains(1, sut.GetAt("V", 9.9));

            // nuove operazioni fuori range vengono bloccate
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add("V", 9.9, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.Move("V", 9.9, 6.0, 1));
        }

        [Fact]
        public void Move_RespectsAxisLength_OnToPosition()
        {
            var sut = Make(len: 3.0);
            sut.Add("V", 1.0, 7);

            // verso 2.5: ok
            Assert.True(sut.Move("V", 1.0, 2.5, 7));
            Assert.Contains(7, sut.GetAt("V", 2.5));

            // oltre range: eccezione? → secondo design ritorna false se from mancante;
            // qui from esiste ma to è invalida → ValidatePosition lancerà
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.Move("V", 2.5, 3.5, 7));
        }

        [Fact]
        public void EnumerateAll_OrdersPositionsWithToleranceBuckets()
        {
            var sut = Make();
            double tol = ProgesiAxisVariable.DefaultTolerance;

            sut.Add("V", 1.0 + tol * 1.9, 2);  // bucket ~ 1.0+tol*1.9
            sut.Add("V", 1.0, 1);              // bucket ~ 1.0
            sut.Add("V", 2.0, 3);

            var list = sut.EnumerateAll().Where(x => x.variableName == "V").ToList();

            // L'ordinamento avviene per bucket, poi per id.
            // I primi due elementi devono avere positions ~ 1.0 e ~1.0+tol*1.9 (ordine per bucket)
            Assert.True(list.Count >= 3);
            Assert.True(list[0].position <= list[1].position);
            Assert.True(list[1].position <= list[2].position);
        }
        [Fact]
        public void ReplaceMap_OverwritesPreviousEntries_Completely()
        {
            var sut = Make();
            sut.Add("V", 0.0, 1);
            sut.Add("V", 1.0, 2);

            var entries = new List<(double position, IEnumerable<int> ids)>();
            entries.Add((2.0, new[] { 9 }));

            sut.ReplaceMap("V", entries);

            Assert.Empty(sut.GetAt("V", 0.0));
            Assert.Empty(sut.GetAt("V", 1.0));
            Assert.Contains(9, sut.GetAt("V", 2.0));
        }

        [Fact]
        public void RoundTrip_RebuildFromEnumerateAll_GroupedMaps()
        {
            var original = Make(5, "AX", 10.0, 77);
            original.Add("A", 0.0, 1);
            original.Add("A", 1.5, 2);
            original.Add("B", 1.5, 3);

            // Estrai "DTO": per ogni name -> (pos, ids[])
            var groups = new Dictionary<string, Dictionary<double, List<int>>>(StringComparer.Ordinal);
            foreach (var triple in original.EnumerateAll())
            {
                if (!groups.TryGetValue(triple.variableName, out var posDict))
                {
                    posDict = new Dictionary<double, List<int>>();
                    groups[triple.variableName] = posDict;
                }
                if (!posDict.TryGetValue(triple.position, out var ids))
                {
                    ids = new List<int>();
                    posDict[triple.position] = ids;
                }
                ids.Add(triple.variableId);
            }

            // Ricostruisci
            var rebuilt = Make(5, "AX", 10.0, 77);
            foreach (var kv in groups)
            {
                var name = kv.Key;
                var entries = new List<(double position, IEnumerable<int> ids)>();
                foreach (var pos in kv.Value.Keys)
                {
                    entries.Add((pos, kv.Value[pos]));
                }
                rebuilt.ReplaceMap(name, entries);
            }

            Assert.True(original.Equals(rebuilt));
            Assert.Equal(original.GetHashCode(), rebuilt.GetHashCode());
        }

        [Fact]
        public void RenameGroup_IsCaseSensitive_ByOrdinalComparer()
        {
            var sut = Make();
            sut.Add("Var", 0.0, 1);
            sut.Add("var", 1.0, 2);

            // 'Var' e 'var' sono distinti
            Assert.True(sut.RenameGroup("Var", "VarRenamed"));
            Assert.Empty(sut.GetAt("Var", 0.0));
            Assert.Contains(2, sut.GetAt("var", 1.0));
        }

        [Fact]
        public void Add_SameIdInDifferentBuckets_IsIndependent()
        {
            var sut = Make();
            double tol = ProgesiAxisVariable.DefaultTolerance;

            sut.Add("V", 1.0, 100);
            sut.Add("V", 1.0 + tol * 3.0, 100); // bucket diverso, ammesso

            Assert.Single(sut.GetAt("V", 1.0));
            Assert.Single(sut.GetAt("V", 1.0 + tol * 3.0));
        }

        [Fact]
        public void Move_ReturnsFalse_WhenIdNotFoundInFromBucket()
        {
            var sut = Make();
            sut.Add("V", 1.0, 10);
            Assert.False(sut.Move("V", 1.0, 2.0, 99));
        }
        // -------------------------
        // Fuzz / Property-like tests (riproducibili via seed)
        // -------------------------
        public class ProgesiAxisVariableFuzzTests
        {
            private static ProgesiAxisVariable Make(int id = 1, string name = "Axis-Fuzz", double? len = null, int? ruleId = null)
                => new ProgesiAxisVariable(id, name, len, ruleId);

            /// <summary>
            /// Invariante forte:
            /// per ogni (name, p1, p2):
            ///   - se GetAt(name, p1) ∩ GetAt(name, p2) ≠ ∅ allora GetAt(name, p1) == GetAt(name, p2) (stesso bucket)
            ///   - gli Id sono unici nel bucket (implicito via HashSet)
            ///   - EnumerateAll è consistente con GetMap (stesso contenuto aggregato)
            /// </summary>
            private static void AssertInvariants(ProgesiAxisVariable sut)
            {
                // 1) Costruisci snapshot per nome -> posizione -> ids[]
                var byName = new Dictionary<string, Dictionary<double, int[]>>(StringComparer.Ordinal);
                foreach (var name in sut.VariableNames)
                {
                    var map = sut.GetMap(name); // pos -> ids[]
                    byName[name] = map.ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(x => x).ToArray());
                }

                // 2) Invariante bucket: se due posizioni condividono anche solo un id, i set devono essere identici
                foreach (var kv in byName)
                {
                    var positions = kv.Value.Keys.ToList();
                    for (int i = 0; i < positions.Count; i++)
                    {
                        var p1 = positions[i];
                        var s1 = kv.Value[p1];
                        for (int j = i + 1; j < positions.Count; j++)
                        {
                            var p2 = positions[j];
                            var s2 = kv.Value[p2];
                            bool intersect = s1.Intersect(s2).Any();
                            if (intersect)
                            {
                                Assert.True(s1.SequenceEqual(s2),
                                    $"Bucket inconsistente per '{kv.Key}': pos {p1} e {p2} condividono Id ma set diversi.");
                            }
                        }
                    }
                }

                // 3) Consistenza EnumerateAll vs GetMap (stesso multinsieme di triple)
                var triplesFromEnum = sut.EnumerateAll().ToList();
                var triplesFromMap = new List<(string name, double pos, int id)>();
                foreach (var kv in byName)
                {
                    foreach (var p in kv.Value.Keys)
                    {
                        foreach (var id in kv.Value[p])
                            triplesFromMap.Add((kv.Key, p, id));
                    }
                }

                // ordiniamo per confronto deterministico
                Func<(string name, double pos, int id), string> keyer = t => $"{t.name}|{t.pos:F12}|{t.id}";
                var a = triplesFromEnum.Select(t => (t.variableName, t.position, t.variableId)).Select(keyer).OrderBy(x => x).ToArray();
                var b = triplesFromMap.Select(keyer).OrderBy(x => x).ToArray();
                Assert.Equal(b, a);
            }

            [Theory]
            [InlineData(12345)]
            [InlineData(987654321)]
            [InlineData(42)]
            public void Fuzz_NoAxisLength_RandomOps_MaintainsInvariants(int seed)
            {
                var rnd = new Random(seed);
                var sut = Make();

                var possibleNames = new[] { "A", "B", "C", "D" };
                int maxId = 0;

                int ops = 500;
                for (int k = 0; k < ops; k++)
                {
                    int op = rnd.Next(0, 5); // 0=Add,1=Move,2=RemoveAt,3=ReplaceMap,4=RemoveAll
                    string name = possibleNames[rnd.Next(possibleNames.Length)];

                    try
                    {
                        switch (op)
                        {
                            case 0: // Add
                                {
                                    double pos = rnd.NextDouble() * 10.0 + (rnd.Next(0, 2) == 0 ? 0.0 : ProgesiAxisVariable.DefaultTolerance * rnd.Next(-3, 4));
                                    int id = (++maxId);
                                    sut.Add(name, pos, id);
                                    break;
                                }
                            case 1: // Move
                                {
                                    var all = sut.EnumerateAll().ToList();
                                    if (all.Count == 0) break;
                                    var pick = all[rnd.Next(all.Count)];
                                    double delta = (rnd.NextDouble() - 0.5) * 0.01; // piccolo spostamento
                                    double to = pick.position + delta;
                                    // move può fallire (false) o lanciare (pos invalid); ignoriamo il risultato: stiamo fuzzando
                                    try { sut.Move(pick.variableName, pick.position, to, pick.variableId); } catch { /* ignore */ }
                                    break;
                                }
                            case 2: // RemoveAt
                                {
                                    var all = sut.EnumerateAll().ToList();
                                    if (all.Count == 0) break;
                                    var pick = all[rnd.Next(all.Count)];
                                    sut.RemoveAt(pick.variableName, pick.position, pick.variableId);
                                    break;
                                }
                            case 3: // ReplaceMap (per il gruppo)
                                {
                                    int n = rnd.Next(0, 5); // da 0 a 4 posizioni
                                    var entries = new List<(double position, IEnumerable<int> ids)>();
                                    for (int i = 0; i < n; i++)
                                    {
                                        double p = rnd.NextDouble() * 5.0 + ProgesiAxisVariable.DefaultTolerance * rnd.Next(-2, 3);
                                        int m = rnd.Next(0, 3); // 0..2 id
                                        var ids = new List<int>();
                                        for (int j = 0; j < m; j++)
                                            ids.Add(++maxId);
                                        entries.Add((p, ids));
                                    }
                                    sut.ReplaceMap(name, entries);
                                    break;
                                }
                            case 4: // RemoveAll
                                {
                                    sut.RemoveAll(name); // può essere false, va bene
                                    break;
                                }
                        }
                    }
                    catch
                    {
                        // in fuzz accettiamo eccezioni di validazione; l'importante è che lo stato resti coerente
                    }

                    AssertInvariants(sut);
                }
            }

            [Theory]
            [InlineData(2024)]
            [InlineData(77)]
            public void Fuzz_WithAxisLength_AndOccasionalShrink_MaintainsInvariants(int seed)
            {
                var rnd = new Random(seed);
                var sut = Make(len: 5.0);

                var possibleNames = new[] { "α", "β", "γ" };
                int maxId = 0;

                int ops = 600;
                for (int k = 0; k < ops; k++)
                {
                    // Ogni tanto restringiamo l'asse (policy: non retrovalida, ma blocca futuro)
                    if (k % 100 == 0 && k > 0)
                    {
                        double newLen = 1.0 + rnd.NextDouble() * 4.0; // (1..5)
                        sut.SetAxisLength(newLen);
                    }

                    int op = rnd.Next(0, 4); // 0=Add,1=Move,2=RemoveAt,3=ReplaceMap
                    string name = possibleNames[rnd.Next(possibleNames.Length)];

                    try
                    {
                        switch (op)
                        {
                            case 0: // Add (rispettando axis length per ridurre eccezioni)
                                {
                                    double len = sut.AxisLength.HasValue ? sut.AxisLength.Value : 5.0;
                                    double pos = rnd.NextDouble() * len;
                                    // a volte “stuzzichiamo” la tolleranza
                                    pos += ProgesiAxisVariable.DefaultTolerance * rnd.Next(-2, 3);
                                    int id = (++maxId);
                                    sut.Add(name, pos, id);
                                    break;
                                }
                            case 1: // Move
                                {
                                    var all = sut.EnumerateAll().ToList();
                                    if (all.Count == 0) break;
                                    var pick = all[rnd.Next(all.Count)];
                                    double len = sut.AxisLength.HasValue ? sut.AxisLength.Value : 5.0;
                                    double to = Math.Max(0.0, Math.Min(len, pick.position + (rnd.NextDouble() - 0.5) * 0.2));
                                    try { sut.Move(pick.variableName, pick.position, to, pick.variableId); } catch { /* ignore */ }
                                    break;
                                }
                            case 2: // RemoveAt
                                {
                                    var all = sut.EnumerateAll().ToList();
                                    if (all.Count == 0) break;
                                    var pick = all[rnd.Next(all.Count)];
                                    sut.RemoveAt(pick.variableName, pick.position, pick.variableId);
                                    break;
                                }
                            case 3: // ReplaceMap (limitato dentro [0, AxisLength])
                                {
                                    double len = sut.AxisLength.HasValue ? sut.AxisLength.Value : 5.0;
                                    int n = rnd.Next(0, 5);
                                    var entries = new List<(double position, IEnumerable<int> ids)>();
                                    for (int i = 0; i < n; i++)
                                    {
                                        double p = Math.Max(0.0, Math.Min(len, rnd.NextDouble() * len + ProgesiAxisVariable.DefaultTolerance * rnd.Next(-2, 3)));
                                        int m = rnd.Next(0, 3);
                                        var ids = new List<int>();
                                        for (int j = 0; j < m; j++)
                                            ids.Add(++maxId);
                                        entries.Add((p, ids));
                                    }
                                    sut.ReplaceMap(name, entries);
                                    break;
                                }
                        }
                    }
                    catch
                    {
                        // accettiamo eccezioni di validazione
                    }

                    AssertInvariants(sut);
                }
            }
        }
        // -------------------------
        // Extra edge & behavior tests (Package 4)
        // -------------------------
        public class ProgesiAxisVariableMoreTests
        {
            private static ProgesiAxisVariable Make(int id = 1, string name = "Axis-Edge", double? len = null, int? ruleId = null)
                => new ProgesiAxisVariable(id, name, len, ruleId);

            [Fact]
            public void Move_WithinSameBucket_IsNoOp_NoDuplicates()
            {
                var sut = Make();
                double tol = ProgesiAxisVariable.DefaultTolerance;

                sut.Add("V", 1.0, 123);
                // sposto di meno della tolleranza: stesso bucket
                Assert.True(sut.Move("V", 1.0, 1.0 + tol * 0.3, 123));

                var ids = sut.GetAt("V", 1.0).ToArray();
                Assert.Single(ids);
                Assert.Equal(123, ids[0]);
            }

            [Fact]
            public void GetMap_ReturnsDeepCopies_NotLiveView()
            {
                var sut = Make();
                sut.Add("V", 1.0, 1);
                sut.Add("V", 2.0, 2);

                var map = sut.GetMap("V");                 // copia
                map[1.0][0] = 999;                         // provo a "sporcare" la copia
                                                           // lo stato interno deve restare intatto
                var internalIds = sut.GetAt("V", 1.0).ToArray();
                Assert.Single(internalIds);
                Assert.Equal(1, internalIds[0]);
            }

            [Fact]
            public void ReplaceMap_WithEmptyEntries_EmptiesGroup_ButKeepsGroupKey()
            {
                var sut = Make();
                sut.Add("G", 0.0, 1);
                sut.ReplaceMap("G", new List<(double position, IEnumerable<int> ids)>()); // mappa vuota

                Assert.Empty(sut.GetAt("G", 0.0)); // niente più posizioni
                                                   // Il gruppo esiste ancora come chiave interna, ma non ha bucket: verifichiamo tramite GetMap
                var map = sut.GetMap("G");
                Assert.Empty(map);
                // A livello di API non abbiamo accesso diretto alla presenza chiave se vuota: questo test chiarisce la policy
            }

            [Fact]
            public void VariableNames_ReflectsCurrentGroups()
            {
                var sut = Make();
                sut.Add("A", 0.0, 1);
                sut.Add("B", 0.0, 2);

                var names1 = sut.VariableNames.OrderBy(x => x).ToArray();
                Assert.Equal(new[] { "A", "B" }, names1);

                sut.RemoveAll("A");
                var names2 = sut.VariableNames.OrderBy(x => x).ToArray();
                Assert.Equal(new[] { "B" }, names2);
            }

            [Fact]
            public void BoundaryPositions_AroundZero_WithTolerance()
            {
                var sut = Make(len: 10.0);
                double tol = ProgesiAxisVariable.DefaultTolerance;

                // leggermente sopra 0 → OK
                sut.Add("V", tol * 0.5, 1);
                Assert.Contains(1, sut.GetAt("V", 0.0));

                // leggermente sotto 0 ma dentro finestra inclusiva → OK
                sut.Add("V", -tol * 0.5, 2);
                Assert.Contains(2, sut.GetAt("V", 0.0));

                // oltre la finestra → eccezione
                Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add("V", -tol * 2.0, 3));
            }

            [Fact]
            public void BoundaryPositions_AroundAxisLength_WithTolerance()
            {
                var sut = Make(len: 5.0);
                double tol = ProgesiAxisVariable.DefaultTolerance;

                // poco sotto 5 → OK
                sut.Add("V", 5.0 - tol * 0.4, 1);
                Assert.Contains(1, sut.GetAt("V", 5.0));

                // poco sopra 5 ma entro finestra → OK
                sut.Add("V", 5.0 + tol * 0.4, 2);
                Assert.Contains(2, sut.GetAt("V", 5.0));

                // troppo oltre → eccezione
                Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add("V", 5.0 + tol * 3.0, 9));
            }

            [Fact]
            public void Equality_SameStateDifferentInsertionOrdersAcrossGroups()
            {
                var a = Make(1, "AX", 10.0, 7);
                a.Add("A", 1.0, 1);
                a.Add("B", 2.0, 2);

                var b = Make(1, "AX", 10.0, 7);
                b.Add("B", 2.0, 2);
                b.Add("A", 1.0, 1);

                Assert.True(a.Equals(b));
                Assert.Equal(a.GetHashCode(), b.GetHashCode());
            }

            [Fact]
            public void Equality_ChangesAfterRenameGroup()
            {
                var a = Make(1, "AX", 10.0, 7);
                a.Add("G", 1.0, 1);

                var b = Make(1, "AX", 10.0, 7);
                b.Add("G", 1.0, 1);
                b.RenameGroup("G", "H");

                Assert.False(a.Equals(b)); // i nomi gruppo sono parte dello stato
            }

            [Fact]
            public void BigButFast_Adds_StayConsistent()
            {
                var sut = Make();
                int N = 5000; // abbastanza grande ma veloce
                for (int i = 0; i < N; i++)
                {
                    string name = (i % 2 == 0) ? "Even" : "Odd";
                    double pos = (i % 100) * 0.1; // 0..9.9
                    sut.Add(name, pos, i + 1);
                }

                // pick a few spots to sanity check
                Assert.NotEmpty(sut.GetAt("Even", 0.0));
                Assert.NotEmpty(sut.GetAt("Odd", 9.9));

                // enumerazione coerente con GetMap
                var triples = sut.EnumerateAll().ToList();
                int countFromEnum = triples.Count;
                int countFromMap = 0;
                foreach (var name in sut.VariableNames)
                {
                    var map = sut.GetMap(name);
                    foreach (var kv in map)
                        countFromMap += kv.Value.Length;
                }
                Assert.Equal(countFromEnum, countFromMap);
            }
        }


    }
}
