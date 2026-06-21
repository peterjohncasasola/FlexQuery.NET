using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlexQuery.NET.Adapters.Kendo.Models;

    /// <summary>
    /// Represents a Kendo UI DataSource request containing filter, sort, paging, grouping, and aggregate operations.
    /// </summary>
    public sealed class KendoRequest
    {
        /// <summary>
        /// Gets or sets the page number (1-based).
        /// </summary>
        [JsonPropertyName("page")]
        public int Page { get; set; }

        /// <summary>
        /// Gets or sets the number of records per page.
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        /// <summary>
        /// Gets or sets the number of records to skip.
        /// </summary>
        [JsonPropertyName("skip")]
        public int Skip { get; set; }

        /// <summary>
        /// Gets or sets the number of records to take.
        /// </summary>
        [JsonPropertyName("take")]
        public int Take { get; set; }

        /// <summary>
        /// Gets or sets the filter criteria.
        /// </summary>
        [JsonPropertyName("filter")]
        public KendoFilter? Filter { get; set; }

        /// <summary>
        /// Gets or sets the sort criteria.
        /// </summary>
        [JsonPropertyName("sort")]
        public List<KendoSortDescriptor> Sort { get; set; } = [];

        /// <summary>
        /// Gets or sets the grouping criteria.
        /// </summary>
        [JsonPropertyName("group")]
        public List<KendoGroupDescriptor>? Group { get; set; }
        
        /// <summary>
        /// Gets or sets the aggregate definitions.
        /// </summary>
        [JsonPropertyName("aggregate")]
        public List<KendoAggregateDescriptor>? Aggregates { get; set; }
    }
