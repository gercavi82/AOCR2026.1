-- Los mensajes con event_key ya se deduplican por uq_email_queue_event_key.
-- La correlacion agrupa el tramite, no identifica un evento individual.
-- Conservar la deduplicacion historica para mensajes sin event_key.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public' AND tablename = 'email_queue'
          AND indexname = 'uq_email_queue_event_key'
          AND indexdef LIKE 'CREATE UNIQUE INDEX%'
    ) THEN
        RAISE EXCEPTION 'Falta el indice unico de event_key; no se modifica la deduplicacion';
    END IF;
END $$;

DROP INDEX IF EXISTS public.uq_email_queue_idempotencia;
CREATE UNIQUE INDEX uq_email_queue_idempotencia
    ON public.email_queue (correlation_id, tipo_notificacion, to_address)
    WHERE correlation_id IS NOT NULL AND event_key IS NULL;
COMMIT;
