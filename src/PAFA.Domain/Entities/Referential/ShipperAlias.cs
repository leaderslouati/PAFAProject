namespace PAFA.Domain.Entities.Referential;

    /// <summary>
    /// Alias d'anonymisation : mapping SSC ↔ Shipper réel.
    /// Utilisé pour les rapports Industry (Schedule 2A) afin de
    /// ne pas exposer l'identité réelle des Shippers.
    /// Un Shipper peut avoir plusieurs alias successifs (rotation).
    /// </summary>
    public class ShipperAlias : BaseEntity
    {
        public int Id { get; set; }

        public Guid ShipperId { get; set; }

        public string AliasCode { get; set; } = string.Empty;

        public Shipper Shipper { get; set; } = null!;
    }

