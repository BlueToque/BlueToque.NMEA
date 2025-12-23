using System;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace BlueToque.NMEA
{
    /// <summary>
    /// Sentence parser type contains information about a sentence parser
    /// this informaiton includes the type, description, and ID of the parser.
    /// </summary>
    [DebuggerDisplay("ID = {ID}")]
    public class SentenceParser
    {
        /// <summary>
        /// Constructor that takes a id, description and parser delegate
        /// </summary>
        /// <param name="id"></param>
        /// <param name="description"></param>
        /// <param name="parser"></param>
        public SentenceParser(string id, string description, SentenceParserDelegate parser)
        {
            ArgumentNullException.ThrowIfNull(parser, nameof(parser));
            ArgumentNullException.ThrowIfNullOrEmpty(id, nameof(id));

            ID = id;
            if (id.Length == 6)
            {
                Talker = id[..3];
                ID = id.Substring(3, 3);
            }

            Description = description;
            Parser = parser;
        }


        #region properties
        /// <summary> </summary>
        public string Talker { get; set; } = "";

        /// <summary>
        /// The method udes to parse this sentence
        /// </summary>
        public SentenceParserDelegate Parser { get; set; }

        /// <summary>
        /// The description of this sentence parser
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The sentence ID of this parser
        /// </summary>
        public string ID { get; set; }

        #endregion
    }

    /// <summary>
    /// A list of sentence parser types, indexed by ID
    /// </summary>
    class SentenceParserList : KeyedCollection<string, SentenceParser>
    {
        protected override string GetKeyForItem(SentenceParser item) => item.ID;

        public SentenceParserList Add(string id, string name, SentenceParserDelegate parser)
        {
            Add(new SentenceParser(id, name, parser));
            return this;
        }

        /// <summary>
        /// Override the contains method to add a "$" if you need it
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public new bool Contains(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            else if (value.Length == 6)
                return base.Contains(value.Substring(3, 3));
            else
                return base.Contains(value);

            //else if (value.StartsWith("$"))
            //    return base.Contains(value);
            //else
            //    return base.Contains("$" + value);
        }
    }
}
