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
                CreateOpuarc01(),
                CreateOidar2(),
                CreateOpiar2(),
                CreateTxdgac(),
                CreateOpsarc(),
                CreateOpcar5(),
                CreateOpcar6(),
                CreateOpcarc()
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
                IncrementalMode = SyncIncrementalMode.WatermarkDateTimeChars,
                DeleteStrategy = DeleteStrategy.TombstoneTable,
                WatermarkDateColumn = "CIAFE1",
                WatermarkTimeColumn = "CIAHO1",
                SoftDeleteSourceColumn = "CIAEST",
                SoftDeleteActiveValue = "AC",
                BatchSize = 2000,
                Notes = "Catálogo de compañías AS400 (DGACDAT.CIAARC). CIARUC = RUC de la compañía. Watermark por CIAFE1+CIAHO1.",
                Columns = MapSame(
                    "CIAOID", "CIACOD", "CIACO2", "CIACO3", "CIANOM", "CIATI1", "CIADIR",
                    "CIARUC", "CIAEMA", "CIATEL", "CIACEL", "CIADI2", "CIATE1", "CIACOR",
                    "CIAREP", "CIANO1", "CIATIP", "CIAEST", "CIACIU", "CIAES1", "CIAOI1",
                    "CIAUSU", "CIAFEC", "CIAHOR", "CIADIS", "CIAUS1", "CIAFE1", "CIAHO1",
                    "CIADI1", "CIADI3", "CIATE2", "CIACE1", "CIAEM1", "CIARE1", "CIAOI2", "CIACO4")
            };
        }

        private static SyncTableDefinition CreateOpuarc01()
        {
            return new SyncTableDefinition
            {
                Name = "OPUARC01",
                SourceSchema = "DGACDAT",
                SourceTable = "OPUARC01",
                TargetSchema = "mirror_raw",
                TargetTable = "opuarc01",
                PrimaryKeys = new List<string> { "opucod" },
                IncrementalMode = SyncIncrementalMode.FullSnapshot,
                DeleteStrategy = DeleteStrategy.FullSnapshotReconcile,
                AllowFullSnapshotDeleteReconcile = true,
                BatchSize = 10000,
                Notes = "Catalogo ubicacion usuario (lugar emision). Snapshot completo para evitar dependencia AS400 en linea.",
                Columns = MapSame("OPUOID", "OPUCOD", "OPUEST")
            };
        }

        private static SyncTableDefinition CreateOidar2()
        {
            return new SyncTableDefinition
            {
                Name = "OIDAR2",
                SourceSchema = "DGACDAT",
                SourceTable = "OIDAR2",
                TargetSchema = "mirror_raw",
                TargetTable = "oidar2",
                PrimaryKeys = new List<string> { "oidco3", "oidoi2" },
                IncrementalMode = SyncIncrementalMode.FullSnapshot,
                DeleteStrategy = DeleteStrategy.FullSnapshotReconcile,
                AllowFullSnapshotDeleteReconcile = true,
                BatchSize = 10000,
                Notes = "Ubicacion aeropuerto por ciudad (fallback para lugar emision). Snapshot completo.",
                Columns = MapSame("OIDOI2", "OIDCO3", "OIDNO2")
            };
        }

        private static SyncTableDefinition CreateOpcarc()
        {
            return new SyncTableDefinition
            {
                Name = "OPCARC",
                SourceSchema = "DGACDAT",
                SourceTable = "OPCARC",
                TargetSchema = "mirror_raw",
                TargetTable = "opcarc",
                PrimaryKeys = new List<string> { "opccod" },
                IncrementalMode = SyncIncrementalMode.WatermarkDateTimeChars,
                WatermarkDateColumn = "OPCDA2",
                WatermarkTimeColumn = "OPCHO4",
                DeleteStrategy = DeleteStrategy.None,
                Enabled = true,
                BatchSize = 2000,
                Notes = "Catalogo de operadores DGAC (OPCARC). Incluye RUC/identificacion fiscal. 5000+ entradas.",
                Columns = MapSame(
                    "OPCCOD", "OPCSIG", "OPCCO1", "OPCNOM", "OPCCO2", "OPCNO1", "OPCDIR",
                    "OPCRUC", "OPCEMA", "OPCREP", "OPCUS3", "OPCDA2", "OPCHO4", "OPCUS4",
                    "OPCDA3", "OPCHO5", "OPCCEL", "OPCTEL")
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
                // SNAP: no hay columna de modificacion; watermark por creacion (OPCDA4+OPCH01).
                // Captura nuevas inserciones. Modificaciones de FR3 son raras (registro contable).
                IncrementalMode = SyncIncrementalMode.WatermarkDateTimeChars,
                WatermarkDateColumn = "OPCDA4",
                WatermarkTimeColumn = "OPCH01",
                DeleteStrategy = DeleteStrategy.None,
                Enabled = true,
                BatchSize = 2000,
                Notes = "FR3 cabecera (OPCAR5). Watermark por OPCDA4+OPCH01 (fecha/hora creacion). Solo captura inserciones nuevas.",
                Columns = MapSame(
                    "OPCSEC","OPCAER","OPCANO","OPCFE4","OPCTIP","OPCRUT","OPCNRO","OPCTOT","OPCGRA",
                    "OPCSON","OPCAUT","OPCOBS","OPCOID","OPCORI","OPCDE7","OPCRET","OPCCAL","OPCEST",
                    "OPCRU1","OPCEM1","OPCNAC","OPCUS7","OPCDA4","OPCH01","OPCOI1","OPCTE1","OPCNO4",
                    "OPCDI3","OPCOI2","OPCVA6","OPCFOR","OPCNO5","OPCMOD","OPCPES","OPCC08","OPCNO6",
                    "OPCEM2","OPCMAT","OPCPRO","OPCSUB","OPCOI3","OPCDI2","OPCFE9","OPCBAN","OPCCHE","OPCNUM")
            };
        }

        private static SyncTableDefinition CreateOpiar2()
        {
            return new SyncTableDefinition
            {
                Name = "OPIAR2",
                SourceSchema = "DGACDAT",
                SourceTable = "OPIAR2",
                TargetSchema = "mirror_raw",
                TargetTable = "opiar2",
                PrimaryKeys = new List<string> { "opiced", "opitip" },
                IncrementalMode = SyncIncrementalMode.FullSnapshot,
                DeleteStrategy = DeleteStrategy.FullSnapshotReconcile,
                AllowFullSnapshotDeleteReconcile = true,
                BatchSize = 10000,
                Notes = "Catalogo de inspectores institucionales (OPS/AIR). Snapshot completo para soporte de consulta RT sin AS400 en linea.",
                Columns = MapSame("OPICED", "OPINO2", "OPIES1", "OPITIP")
            };
        }

        private static SyncTableDefinition CreateTxdgac()
        {
            return new SyncTableDefinition
            {
                Name = "TXDGAC",
                SourceSchema = "DGACSYS",
                SourceTable = "TXDGAC",
                TargetSchema = "mirror_raw",
                TargetTable = "txdgac",
                PrimaryKeys = new List<string> { "valdds", "valval" },
                IncrementalMode = SyncIncrementalMode.FullSnapshot,
                DeleteStrategy = DeleteStrategy.FullSnapshotReconcile,
                AllowFullSnapshotDeleteReconcile = true,
                BatchSize = 10000,
                Notes = "Tabla de listas de valores AS400/P9 (bancos, formas de pago y otros catalogos).",
                Columns = MapSame("VALDDS", "VALVAL", "VALDES")
            };
        }

        private static SyncTableDefinition CreateOpsarc()
        {
            return new SyncTableDefinition
            {
                Name = "OPSARC",
                SourceSchema = "DGACDAT",
                SourceTable = "OPSARC",
                TargetSchema = "mirror_raw",
                TargetTable = "opsarc",
                PrimaryKeys = new List<string> { "opsaer", "opsano" },
                IncrementalMode = SyncIncrementalMode.FullSnapshot,
                DeleteStrategy = DeleteStrategy.FullSnapshotReconcile,
                AllowFullSnapshotDeleteReconcile = true,
                BatchSize = 2000,
                Notes = "Secuenciales FR3 por aeropuerto/anio. Se replica para trazabilidad y contingencia de numeracion.",
                Columns = MapSame("OPSAER", "OPSANO", "OPSSEC")
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
                // SNAP: OPCAR6 no tiene columnas fecha/hora de modificacion.
                // Estrategia: FullSnapshot + reconcile deletes. Batch grande para leer todo de una vez.
                // ATENCION: si la tabla es muy grande (>100k filas), reducir BatchSize o desactivar hasta optimizar.
                IncrementalMode = SyncIncrementalMode.FullSnapshot,
                DeleteStrategy = DeleteStrategy.FullSnapshotReconcile,
                AllowFullSnapshotDeleteReconcile = true,
                Enabled = true,
                BatchSize = 10000,
                Notes = "FR3 detalle (OPCAR6). FullSnapshot+reconcile deletes. Sin columnas de watermark en el SNAP.",
                Columns = MapSame(
                    "OPCSE2","OPCAE1","OPCAN1","OPCSE1","OPCTI1","OPCOI4","OPCC05","OPCDE8",
                    "OPCCAN","OPCVA1","OPCDE9","OPCIMP","OPCHAC","OPCPOR","OPCCOB","OPCPO1",
                    "OPCING","OPCD01","OPCVA2","OPCVA3","OPCVA4","OPCC06","OPCVA5","OPCTO1","OPCUBI")
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
