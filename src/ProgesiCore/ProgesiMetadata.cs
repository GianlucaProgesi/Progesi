using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgesiCore
{
    public sealed class ProgesiMetadata : ValueObject
    {
        private readonly List<string> _references = new List<string>();
        private readonly List<ProgesiSnip> _snips = new List<ProgesiSnip>();

        private ProgesiMetadata() { } // per serializer

        private ProgesiMetadata(
            int id,
            DateTime lastModified,
            string createdBy,
            string? additionalInfo,
            IEnumerable<Uri>? references,
            IEnumerable<ProgesiSnip>? snips)
        {
            if (id <= 0)
            {
                throw new ArgumentException("Id must be positive.", nameof(id));
            }

            Id = id;
            LastModified = lastModified;
            CreatedBy = !string.IsNullOrWhiteSpace(createdBy) ? createdBy : throw new ArgumentNullException(nameof(createdBy));
            AdditionalInfo = additionalInfo ?? string.Empty;

            if (references != null)
            {
                foreach (Uri r in references)
                {
                    if (r == null)
                    {
                        continue;
                    }

                    _references.Add(r.ToString());
                }
            }

            if (snips != null)
            {
                foreach (ProgesiSnip s in snips)
                {
                    // Evita l'overload dell'operator != su ValueObject in C# 8
                    if (s is object)
                    {
                        _snips.Add(s);
                    }
                }
            }
        }

        public static ProgesiMetadata Create(
            string createdBy,
            string? additionalInfo = null,
            IEnumerable<Uri>? references = null,
            IEnumerable<ProgesiSnip>? snips = null,
            DateTime? lastModifiedUtc = null,
            int? id = null)
        {
            return new ProgesiMetadata(
                id ?? 0,
                lastModifiedUtc ?? DateTime.UtcNow,
                createdBy,
                additionalInfo,
                references,
                snips);
        }

        /// <summary>
        /// Identifier (analogo a ProgesiVariable).
        /// </summary>
        public int Id { get; private set; }

        public DateTime LastModified { get; private set; }
        public string CreatedBy { get; private set; } = string.Empty;
        public string AdditionalInfo { get; private set; } = string.Empty;

        public IReadOnlyList<Uri> References =>
            _references.Select(s => new Uri(s, UriKind.RelativeOrAbsolute)).ToList();

        public IReadOnlyList<ProgesiSnip> Snips => _snips.ToList();

        public void UpdateAdditionalInfo(string? info)
        {
            AdditionalInfo = info ?? string.Empty;
            Touch();
        }

        public void AddReference(Uri reference)
        {
            if (reference == null)
            {
                return;
            }

            string s = reference.ToString();
            if (!_references.Contains(s))
            {
                _references.Add(s);
            }

            Touch();
        }

        public void AddReferences(IEnumerable<Uri> references)
        {
            if (references == null)
            {
                return;
            }

            foreach (Uri r in references)
            {
                if (r == null)
                {
                    continue;
                }

                string s = r.ToString();
                if (!_references.Contains(s))
                {
                    _references.Add(s);
                }
            }
            Touch();
        }

        public bool RemoveReference(Uri reference)
        {
            if (reference == null)
            {
                return false;
            }

            bool removed = _references.Remove(reference.ToString());
            if (removed)
            {
                Touch();
            }

            return removed;
        }

        public ProgesiSnip AddSnip(byte[] content, string mimeType, string? caption = null, Uri? source = null)
        {
            var snip = ProgesiSnip.Create(content, mimeType, caption, source);
            _snips.Add(snip);
            Touch();
            return snip;
        }

        public bool RemoveSnip(Guid snipId)
        {
            int idx = _snips.FindIndex(s => s.Id == snipId);
            if (idx < 0)
            {
                return false;
            }

            _snips.RemoveAt(idx);
            Touch();
            return true;
        }

        public void Touch()
        {
            LastModified = DateTime.UtcNow;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Id;
            yield return CreatedBy;
            yield return AdditionalInfo;

            foreach (string r in _references)
            {
                yield return r;
            }

            foreach (ProgesiSnip s in _snips)
            {
                yield return s;
            }

            yield return LastModified;
        }
    }

    public sealed class ProgesiSnip : ValueObject
    {
        private ProgesiSnip() { } // per serializer

        private ProgesiSnip(Guid id, byte[] content, string mimeType, string? caption, string? source)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Id must not be empty.", nameof(id));
            }

            if (content == null || content.Length == 0)
            {
                throw new ArgumentException("Content must not be empty.", nameof(content));
            }

            if (string.IsNullOrWhiteSpace(mimeType))
            {
                throw new ArgumentNullException(nameof(mimeType));
            }

            Id = id;
            Content = content;
            MimeType = mimeType;
            Caption = caption ?? string.Empty;
            Source = source;
        }

        public static ProgesiSnip Create(byte[] content, string mimeType, string? caption = null, Uri? source = null)
        {
            return new ProgesiSnip(Guid.NewGuid(), content, mimeType, caption, source?.ToString());
        }

        public Guid Id { get; private set; }
        public byte[] Content { get; private set; } = Array.Empty<byte>();
        public string MimeType { get; private set; } = "image/png";
        public string Caption { get; private set; } = string.Empty;
        public string? Source { get; private set; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Id;
            yield return MimeType;
            yield return Caption;
            yield return Source ?? string.Empty;
        }
    }
}
