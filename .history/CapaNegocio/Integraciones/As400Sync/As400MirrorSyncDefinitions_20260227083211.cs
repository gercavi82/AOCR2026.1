using System.Collections.Generic;

namespace CapaNegocio.Integraciones.As400Sync
{
    public static class As400MirrorSyncDefinitions
    {
        public static IList<SyncTableDefinition> CreateDefault()
        {
            return new List<SyncTableDefinition>
            {
                CreateUsuarc(),
                CreateUsuar1(),
                CreateCiaarc(),
                CreateOpcar5(),
                CreateOpcar6()
            };
        }

        private static SyncTableDefinition CreateUsuarc()
        {
            return new SyncTableDefinition
            {
                Name = "USUARC",
                SourceSchema = "DGACDAT",
                SourceTable = "USUARC",
                TargetSchema = "mirror_raw",
                TargetTable = "usuarc",
                PrimaryKeys = new List<string> { "usucod" },
                IncrementalMode = SyncIncrementalMode.WatermarkDateTimeChars,
                DeleteStrategy = DeleteStrategy.TombstoneTable,
                WatermarkDateColumn = "USUFE1",
                WatermarkTimeColumn = "USUHO1",
                SoftDeleteSourceColumn = "USUEST",
                SoftDeleteActiveValue = "AC",
                BatchSize = 1000,
                Notes = "Usuarios AS400 (USUARIO/USUARC). Watermark por USUFE1+USUHO1.",
                Columns = MapSame(
                    "USUCOD","USUNOM","USUAPE","USUTIP","USUCED","USUCOR","USUCLA","USUEST","USUTI1",
                    "USUIDE","USUNUM","USUAUX","USUAU1","USUAU2","USUUSU","USUFEC","USUHOR","USUDIS",
                    "USUUS1","USUFE1","USUHO1","USUDI1","USUCO1","USUCO2","USUCO3","USUCO4","USUCO5","USUCO6")
            };
        }

        private static SyncTableDefinition CreateUsuar1()
        {
            return new SyncTableDefinition
            {
                Name = "USUAR1",
                SourceSchema = "DGACDAT",
                SourceTable = "USUAR1",
                TargetSchema = "mirror_raw",
                TargetTable = "usuar1",
                PrimaryKeys = new List<string> { "usuco8" },
                IncrementalMode = SyncIncrementalMode.WatermarkDateTimeChars,
                DeleteStrategy = DeleteStrategy.TombstoneTable,
                WatermarkDateColumn = "USUFE3",
                WatermarkTimeColumn = "USUHO3",
                BatchSize = 1000,
                Notes = "Usuario adicional AS400 (USUAR1). Watermark por USUFE3+USUHO3.",
                Columns = MapSame(
                    "USUCO8","USUTIT","USUTI2","USUNO1","USUCAR","USUNU1","USUNU2","USUCO7",
                    "USUUS2","USUFE2","USUHO2","USUDI2","USUUS3","USUFE3","USUHO3","USUDI3","USUOID","USUCO9")
            };
        }

        private static SyncTableDefinition CreateCiaarc()
        {
            return new SyncTableDefinition
            {
                Name = "CIAARC",
                SourceSchema = "DGACDAT",
                SourceTable = "CIAARC",
                TargetSchema = "mirror_raw",
                TargetTable = "ciaarc",
                PrimaryKeys = new List<string> { "ciacod" },
                IncrementalMode = SyncIncrementalMode.FullSnapshot,
                DeleteStrategy = DeleteStrategy.FullSnapshotReconcile,
                SoftDeleteSourceColumn = "CIAEST",
                SoftDeleteActiveValue = "AC",
                BatchSize = 10000,
                Notes = "Catalogo de companias. Sin watermark confiable en AOCR actual; usa snapshot + reconcile de deletes.",
                Columns = MapSame("CIACOD", "CIACO2", "CIACO3", "CIANOM", "CIAEST")
            };
        }

        private static SyncTableDefinition CreateOpcar5()
        {
            return new SyncTableDefinition
            {
                Name = "OPCAR5",
                SourceSchema = "DGACDAT",
                SourceTable = "OPCAR5",
                TargetSchema = "mirror_raw",
                TargetTable = "opcar5",
                PrimaryKeys = new List<string> { "opcsec", "opcaer", "opcano" },
                // OPCAR5 no tiene columna de modificación. Usa OPCDA4+OPCH01 (DATE/HORA CR) como watermark.
                // Los registros FR3 son insert-only: este watermark es seguro y correcto.
                IncrementalMode = SyncIncrementalMode.WatermarkDateTimeChars,
                DeleteStrategy = DeleteStrategy.None,
                Enabled = true,
                WatermarkDateColumn = "OPCDA4",
                WatermarkTimeColumn = "OPCH01",
                BatchSize = 2000,
                Notes = "FR3 cabecera OPCAR5. Watermark por OPCDA4+OPCH01 (fecha/hora creacion). Registros son insert-only.",
                Columns = MapSame(
                    "OPCSEC","OPCAER","OPCANO","OPCFE4","OPCTIP","OPCRUT","OPCNRO","OPCTOT","OPCGRA",
                    "OPCSON","OPCAUT","OPCOBS","OPCOID","OPCORI","OPCDE7","OPCRET","OPCCAL","OPCEST",
                    "OPCRU1","OPCEM1","OPCNAC","OPCUS7","OPCDA4","OPCH01","OPCOI1","OPCTE1","OPCNO4",
                    "OPCDI3","OPCOI2","OPCVA6","OPCFOR","OPCNO5","OPCMOD","OPCPES","OPCC08","OPCNO6",
                    "OPCEM2","OPCMAT","OPCPRO","OPCSUB","OPCOI3","OPCDI2","OPCFE9","OPCBAN","OPCCHE","OPCNUM")
            };
        }

        private static SyncTableDefinition CreateOpcar6()
        {
            return new SyncTableDefinition
            {
                Name = "OPCAR6",
                SourceSchema = "DGACDAT",
                SourceTable = "OPCAR6",
                TargetSchema = "mirror_raw",
                TargetTable = "opcar6",
                PrimaryKeys = new List<string> { "opcse2", "opcae1", "opcan1", "opcse1" },
                IncrementalMode = SyncIncrementalMode.Disabled,
                DeleteStrategy = DeleteStrategy.None,
                Enabled = false,
                BatchSize = 5000,
                Notes = "FR3 detalle. Deshabilitada por defecto hasta validar estrategia incremental dependiente de OPCAR5.",
                Columns = MapSame("OPCSE2","OPCAE1","OPCAN1","OPCSE1","OPCTI1","OPCOI4","OPCC05","OPCDE8","OPCCAN","OPCVA1","OPCHAC","OPCCOB","OPCING","OPCD01","OPCC06","OPCTO1")
            };
        }

        private static IList<MirrorColumnDefinition> MapSame(params string[] cols)
        {
            var list = new List<MirrorColumnDefinition>();
            foreach (var col in cols)
            {
                list.Add(new MirrorColumnDefinition
                {
                    SourceColumn = col,
                    TargetColumn = col.ToLowerInvariant(),
                    TrimString = true
                });
            }
            return list;
        }
    }
}
