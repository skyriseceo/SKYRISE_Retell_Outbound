--
-- PostgreSQL database dump
--

\restrict 0gvKwstbKwBvoVRUHXP9xdJrKEObkB3bNDEw3dUSWWsXaAykewnDmqR5ybSNBPs

-- Dumped from database version 17.6
-- Dumped by pg_dump version 17.6

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

DROP DATABASE "OSV";
--
-- Name: OSV; Type: DATABASE; Schema: -; Owner: postgres
--

CREATE DATABASE "OSV" WITH TEMPLATE = template0 ENCODING = 'UTF8' LOCALE_PROVIDER = libc LOCALE = 'Arabic_Egypt.1252';


ALTER DATABASE "OSV" OWNER TO postgres;

\unrestrict 0gvKwstbKwBvoVRUHXP9xdJrKEObkB3bNDEw3dUSWWsXaAykewnDmqR5ybSNBPs
\connect "OSV"
\restrict 0gvKwstbKwBvoVRUHXP9xdJrKEObkB3bNDEw3dUSWWsXaAykewnDmqR5ybSNBPs

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: fn_add_booking(character varying, character varying, character varying, timestamp with time zone, character varying, character varying, integer); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_add_booking(p_name character varying, p_email character varying, p_phone character varying, p_appointment_time timestamp with time zone, p_agent_id character varying, p_call_id character varying, p_status integer) RETURNS bigint
    LANGUAGE plpgsql
    AS $$
DECLARE
    new_booking_id BIGINT;
    v_customer_id BIGINT;
BEGIN
    
    -- الخطوة 1: (كما طلبت) نبحث عن العميل بالإيميل أولاً
    SELECT id INTO v_customer_id
    FROM public.customers
    WHERE lower(email) = lower(p_email); -- (نستخدم lower لضمان المقارنة)

    IF FOUND THEN
        UPDATE public.customers
        SET 
            status = 2, -- (Booked)
            updated_at = NOW()
        WHERE id = v_customer_id;
    ELSE

        INSERT INTO public.customers (name, email, phone_number, status, created_at, updated_at)
        VALUES (p_name, p_email, p_phone, 2, NOW(), NOW())
        RETURNING id INTO v_customer_id;
    END IF;

    -- الخطوة 4: (باقي الكود كما هو)
    -- إضافة الحجز وربطه بالـ v_customer_id الذي حصلنا عليه
    IF p_status NOT IN (0, 1, 2) THEN
        p_status := 0; 
    END IF;

    INSERT INTO public.bookings (
        appointment_time, 
        agent_id, 
        call_id,
        status,
        created_at,
        updated_at,
        customer_id
    )
    VALUES (
        p_appointment_time, 
        p_agent_id, 
        p_call_id,
        p_status,
        NOW(),
        NOW(),
        v_customer_id -- (الـ ID الصحيح من الخطوات السابقة)
    )
    RETURNING booking_id INTO new_booking_id;

    RETURN new_booking_id;

EXCEPTION 
    WHEN unique_violation THEN
        -- (هذا الـ Exception الآن سيعالج فقط تكرار الـ call_id في جدول bookings)
        RAISE NOTICE 'Duplicate booking attempt for call_id: %', p_call_id;
        RETURN 0; 
END;
$$;


ALTER FUNCTION public.fn_add_booking(p_name character varying, p_email character varying, p_phone character varying, p_appointment_time timestamp with time zone, p_agent_id character varying, p_call_id character varying, p_status integer) OWNER TO postgres;

--
-- Name: fn_add_customer(character varying, character varying, character varying); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_add_customer(p_name character varying, p_phone_number character varying, p_email character varying) RETURNS bigint
    LANGUAGE plpgsql
    AS $$
DECLARE new_customer_id BIGINT;
BEGIN
    INSERT INTO public.customers (name, phone_number, email, status)
    VALUES (p_name, p_phone_number, p_email, 0)
    RETURNING id INTO new_customer_id;
    RETURN new_customer_id;
END;
$$;


ALTER FUNCTION public.fn_add_customer(p_name character varying, p_phone_number character varying, p_email character varying) OWNER TO postgres;

--
-- Name: fn_add_customers_bulk(jsonb); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_add_customers_bulk(p_customers_json jsonb) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
    inserted_count integer;
BEGIN
    WITH new_customers AS (
        SELECT
            -- نقرأ المفاتيح بنفس الـ casing اللي جاي من C#
            (j->>'name') AS name,
            (j->>'phoneNumber') AS phone_number,
            (j->>'email') AS email
        FROM jsonb_array_elements(p_customers_json) AS j
    ),
    inserted AS (
        INSERT INTO public.customers (name, phone_number, email)
        SELECT 
            nc.name, 
            nc.phone_number, 
            nc.email
        FROM new_customers nc
        ON CONFLICT (phone_number) DO NOTHING
        RETURNING 1
    )
    SELECT count(*) INTO inserted_count FROM inserted;

    RETURN inserted_count;

EXCEPTION 
    WHEN OTHERS THEN
        RAISE WARNING 'Error in fn_add_customers_bulk: %', SQLERRM;
        RETURN 0;
END;
$$;


ALTER FUNCTION public.fn_add_customers_bulk(p_customers_json jsonb) OWNER TO postgres;

--
-- Name: fn_delete_customer(bigint); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_delete_customer(p_customer_id bigint) RETURNS boolean
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_booking_count integer;
BEGIN
    -- أولاً: نتحقق هل العميل عنده حجوزات
    SELECT COUNT(*) INTO v_booking_count
    FROM public.bookings
    WHERE customer_id = p_customer_id;

    IF v_booking_count > 0 THEN
        -- لو عنده حجوزات، منرجع false بدون حذف
        RAISE NOTICE 'Cannot delete customer % because they have % bookings.', p_customer_id, v_booking_count;
        RETURN FALSE;
    END IF;

    -- ثانياً: نحذف العميل لو مفيش حجوزات
    DELETE FROM public.customers
    WHERE id = p_customer_id;

    IF FOUND THEN
        RETURN TRUE;
    ELSE
        RETURN FALSE;
    END IF;
END;
$$;


ALTER FUNCTION public.fn_delete_customer(p_customer_id bigint) OWNER TO postgres;

--
-- Name: fn_get_all_customers(integer, integer, character varying, integer); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_get_all_customers(p_page_number integer DEFAULT 1, p_page_size integer DEFAULT 25, p_search_term character varying DEFAULT NULL::character varying, p_status integer DEFAULT NULL::integer) RETURNS TABLE(id bigint, name character varying, phonenumber character varying, email character varying, status integer, createdat timestamp with time zone, updatedat timestamp with time zone, totalcount bigint)
    LANGUAGE plpgsql STABLE PARALLEL SAFE
    AS $$
DECLARE 
    v_offset INT;
    v_page_size INT;
    v_search_term_like character varying;
BEGIN
    v_search_term_like := '%' || COALESCE(p_search_term, '') || '%';
    v_page_size := LEAST(GREATEST(1, p_page_size), 100);
    v_offset := (GREATEST(1, p_page_number) - 1) * v_page_size;

    RETURN QUERY
    WITH FilteredData AS (
        SELECT c.*
        FROM public.customers c
        WHERE
            (p_search_term IS NULL OR p_search_term = '' OR 
             c.name ILIKE v_search_term_like OR 
             c.phone_number LIKE v_search_term_like)
        AND
            (p_status IS NULL OR c.status = p_status)
    ),
    CountedData AS (
        SELECT *, COUNT(*) OVER() AS "TotalCount" FROM FilteredData
    )
    SELECT 
        d.id,
        d.name,
        d.phone_number AS phonenumber,
        d.email,
        d.status,
        d.created_at AS createdat,
        d.updated_at AS updatedat,
        d."TotalCount" AS totalcount
    FROM CountedData d
    ORDER BY d.created_at DESC
    LIMIT v_page_size OFFSET v_offset;
END;
$$;


ALTER FUNCTION public.fn_get_all_customers(p_page_number integer, p_page_size integer, p_search_term character varying, p_status integer) OWNER TO postgres;

--
-- Name: fn_get_booking_by_call_id(character varying); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_get_booking_by_call_id(p_call_id character varying) RETURNS TABLE("BookingId" bigint, "ProspectName" character varying, "ProspectEmail" character varying, "ProspectPhone" character varying, "AppointmentTime" timestamp with time zone, "Status" integer, "AgentId" character varying, "CallId" character varying, "CreatedAt" timestamp with time zone, "CustomerId" bigint)
    LANGUAGE plpgsql STABLE PARALLEL SAFE
    AS $$
BEGIN
    RETURN QUERY
    SELECT 
        b.booking_id AS "BookingId",
        c.name AS "ProspectName",
        c.email AS "ProspectEmail",
        c.phone_number AS "ProspectPhone",
        b.appointment_time AS "AppointmentTime",
        b.status AS "Status",
        b.agent_id AS "AgentId",
        b.call_id AS "CallId",
        b.created_at AS "CreatedAt",
        b.customer_id AS "CustomerId"
    FROM public.bookings b
    JOIN public.customers c ON b.customer_id = c.id
    WHERE b.call_id = p_call_id; -- (التغيير هنا)
END;
$$;


ALTER FUNCTION public.fn_get_booking_by_call_id(p_call_id character varying) OWNER TO postgres;

--
-- Name: fn_get_booking_by_id(bigint); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_get_booking_by_id(p_booking_id bigint) RETURNS TABLE("BookingId" bigint, "ProspectName" character varying, "ProspectEmail" character varying, "ProspectPhone" character varying, "AppointmentTime" timestamp with time zone, "Status" integer, "AgentId" character varying, "CallId" character varying, "CreatedAt" timestamp with time zone, "CustomerId" bigint)
    LANGUAGE plpgsql STABLE PARALLEL SAFE
    AS $$
BEGIN
    RETURN QUERY
    SELECT 
        b.booking_id AS "BookingId",
        
        -- [!!! التعديل الأساسي هنا !!!]
        -- جلب البيانات مباشرة من جدول العملاء وإعطائها الأسماء المستعارة للـ DTO
        c.name AS "ProspectName",
        c.email AS "ProspectEmail",
        c.phone_number AS "ProspectPhone",
        
        b.appointment_time AS "AppointmentTime",
        b.status AS "Status",
        b.agent_id AS "AgentId",
        b.call_id AS "CallId",
        b.created_at AS "CreatedAt",
        b.customer_id AS "CustomerId"
    FROM public.bookings b
    -- استخدام INNER JOIN لأننا نضمن وجود العميل (جعلنا customer_id NOT NULL)
    JOIN public.customers c ON b.customer_id = c.id
    WHERE b.booking_id = p_booking_id;
END;
$$;


ALTER FUNCTION public.fn_get_booking_by_id(p_booking_id bigint) OWNER TO postgres;

--
-- Name: fn_get_booking_statistics(); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_get_booking_statistics() RETURNS TABLE(totalcount bigint, weekcount bigint, todaycount bigint)
    LANGUAGE plpgsql STABLE PARALLEL SAFE
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(*) AS totalcount,
        COUNT(*) FILTER (WHERE created_at >= NOW() - INTERVAL '7 days') AS weekcount,
        COUNT(*) FILTER (WHERE created_at::date = CURRENT_DATE) AS todaycount
    FROM public.bookings;
END;
$$;


ALTER FUNCTION public.fn_get_booking_statistics() OWNER TO postgres;

--
-- Name: fn_get_bookings_paginated(integer, integer, character varying, integer); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_get_bookings_paginated(p_page_number integer DEFAULT 1, p_page_size integer DEFAULT 25, p_search_term character varying DEFAULT NULL::character varying, p_status integer DEFAULT NULL::integer) RETURNS TABLE("BookingId" bigint, "ProspectName" character varying, "ProspectEmail" character varying, "ProspectPhone" character varying, "AppointmentTime" timestamp with time zone, "Status" integer, "AgentId" character varying, "CallId" character varying, "CreatedAt" timestamp with time zone, "CustomerId" bigint, "TotalCount" bigint)
    LANGUAGE plpgsql STABLE PARALLEL SAFE
    AS $$
DECLARE
    v_offset INT;
    v_page_size INT;
    v_search_term_like character varying;
BEGIN
    v_search_term_like := '%' || COALESCE(p_search_term, '') || '%';
    v_page_size := LEAST(GREATEST(1, p_page_size), 100);
    v_offset := (GREATEST(1, p_page_number) - 1) * v_page_size;

    RETURN QUERY
    WITH FilteredData AS (
        SELECT 
            b.*,
            -- جلب البيانات الأساسية من جدول العملاء
            c.name,
            c.email,
            c.phone_number
        FROM public.bookings b
        -- استخدام INNER JOIN
        JOIN public.customers c ON b.customer_id = c.id
        WHERE
            (p_search_term IS NULL OR p_search_term = '' OR 
             -- [!!! تعديل منطق البحث !!!]
             -- البحث في جدول العملاء مباشرة
             c.name ILIKE v_search_term_like OR 
             c.phone_number LIKE v_search_term_like)
        AND
            (p_status IS NULL OR b.status = p_status)
    ),
    CountedData AS (
        SELECT *, COUNT(*) OVER() AS "TotalCount" FROM FilteredData
    )
    SELECT 
        d.booking_id AS "BookingId",
        
        -- [!!! التعديل الأساسي هنا !!!]
        -- إرجاع بيانات العميل بأسماء الـ DTO
        d.name AS "ProspectName",
        d.email AS "ProspectEmail",
        d.phone_number AS "ProspectPhone",
        
        d.appointment_time AS "AppointmentTime",
        d.status AS "Status",
        d.agent_id AS "AgentId",
        d.call_id AS "CallId",
        d.created_at AS "CreatedAt",
        d.customer_id AS "CustomerId",
        d."TotalCount"
    FROM CountedData d
    ORDER BY d.appointment_time DESC
    LIMIT v_page_size OFFSET v_offset;
END;
$$;


ALTER FUNCTION public.fn_get_bookings_paginated(p_page_number integer, p_page_size integer, p_search_term character varying, p_status integer) OWNER TO postgres;

--
-- Name: fn_get_customer_by_id(bigint); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_get_customer_by_id(p_customer_id bigint) RETURNS TABLE(id bigint, name character varying, phonenumber character varying, email character varying, status integer, createdat timestamp with time zone, updatedat timestamp with time zone)
    LANGUAGE plpgsql STABLE PARALLEL SAFE
    AS $$
BEGIN
    RETURN QUERY
    SELECT 
        c.id AS Id,
        c.name AS Name,
        c.phone_number AS PhoneNumber,
        c.email AS Email,
        c.status AS Status,
        c.created_at AS CreatedAt,
        c.updated_at AS UpdatedAt
    FROM public.customers c 
    WHERE c.id = p_customer_id;
END;
$$;


ALTER FUNCTION public.fn_get_customer_by_id(p_customer_id bigint) OWNER TO postgres;

--
-- Name: fn_get_customer_by_phone(character varying); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_get_customer_by_phone(p_phone_number character varying) RETURNS TABLE(id bigint, name character varying, phonenumber character varying, email character varying, status integer, createdat timestamp with time zone, updatedat timestamp with time zone)
    LANGUAGE plpgsql STABLE
    AS $$
BEGIN
    RETURN QUERY
    SELECT 
        c.id AS Id,
        c.name AS Name,
        c.phone_number AS PhoneNumber,
        c.email AS Email,
        c.status AS Status,
        c.created_at AS CreatedAt,
        c.updated_at AS UpdatedAt
    FROM public.customers c 
    WHERE c.phone_number = p_phone_number;
END;
$$;


ALTER FUNCTION public.fn_get_customer_by_phone(p_phone_number character varying) OWNER TO postgres;

--
-- Name: fn_get_customer_statistics(); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_get_customer_statistics() RETURNS TABLE("TotalCount" bigint, "BookedCount" bigint, "CallingCount" bigint, "NewCount" bigint)
    LANGUAGE plpgsql STABLE PARALLEL SAFE
    AS $$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(*) AS "TotalCount",
        COUNT(*) FILTER (WHERE status = 2) AS "BookedCount",      -- Booked
        COUNT(*) FILTER (WHERE status = 1) AS "CallingCount",     -- Calling
        COUNT(*) FILTER (WHERE status = 0) AS "NewCount"          -- New
    FROM public.customers;
END;
$$;


ALTER FUNCTION public.fn_get_customer_statistics() OWNER TO postgres;

--
-- Name: fn_get_customers_by_status(integer); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_get_customers_by_status(p_status integer) RETURNS TABLE(id bigint, name character varying, phonenumber character varying, email character varying, status integer, createdat timestamp with time zone, updatedat timestamp with time zone)
    LANGUAGE plpgsql STABLE PARALLEL SAFE
    AS $$
BEGIN
    RETURN QUERY
    SELECT 
        c.id AS Id,
        c.name AS Name,
        c.phone_number AS PhoneNumber,
        c.email AS Email,
        c.status AS Status,
        c.created_at AS CreatedAt,
        c.updated_at AS UpdatedAt
    FROM public.customers c
    WHERE c.status = p_status
    ORDER BY c.created_at DESC;
END;
$$;


ALTER FUNCTION public.fn_get_customers_by_status(p_status integer) OWNER TO postgres;

--
-- Name: fn_reschedule_booking_by_call_id(character varying, timestamp with time zone); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_reschedule_booking_by_call_id(p_call_id character varying, p_new_start_time timestamp with time zone) RETURNS boolean
    LANGUAGE plpgsql
    AS $$
BEGIN
    UPDATE public.bookings
    SET 
        appointment_time = p_new_start_time,
        status = 1, -- (هام) إعادة الحالة إلى "Confirmed"
        updated_at = NOW()
    WHERE call_id = p_call_id;

    RETURN FOUND;
END;
$$;


ALTER FUNCTION public.fn_reschedule_booking_by_call_id(p_call_id character varying, p_new_start_time timestamp with time zone) OWNER TO postgres;

--
-- Name: fn_update_booking_status(bigint, integer); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_update_booking_status(p_booking_id bigint, p_status integer) RETURNS boolean
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF p_status NOT IN (0, 1, 2) THEN
        RAISE WARNING 'Invalid status code provided: %', p_status;
        RETURN FALSE;
    END IF;

    UPDATE public.bookings
    SET 
        status = p_status,
        updated_at = NOW()
    WHERE booking_id = p_booking_id;

    RETURN FOUND;
END;
$$;


ALTER FUNCTION public.fn_update_booking_status(p_booking_id bigint, p_status integer) OWNER TO postgres;

--
-- Name: fn_update_booking_status_by_call_id(character varying, integer); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_update_booking_status_by_call_id(p_call_id character varying, p_new_status integer) RETURNS boolean
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_booking_id BIGINT;
BEGIN
    -- البحث عن الـ booking_id باستخدام الـ call_id
    SELECT booking_id INTO v_booking_id
    FROM public.bookings
    WHERE call_id = p_call_id;

    IF NOT FOUND THEN
        RAISE WARNING 'No booking found with call_id: %', p_call_id;
        RETURN FALSE;
    END IF;

    -- استخدام الدالة الحالية لتحديث الحالة
    -- (نفترض أن fn_update_booking_status لديك)
    RETURN public.fn_update_booking_status(v_booking_id, p_new_status);
END;
$$;


ALTER FUNCTION public.fn_update_booking_status_by_call_id(p_call_id character varying, p_new_status integer) OWNER TO postgres;

--
-- Name: fn_update_customer(bigint, character varying, character varying, character varying, integer); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_update_customer(p_customer_id bigint, p_name character varying, p_phone_number character varying, p_email character varying, p_status integer) RETURNS boolean
    LANGUAGE plpgsql
    AS $$
BEGIN
    UPDATE public.customers
    SET 
        name = p_name,
        phone_number = p_phone_number,
        email = p_email,
        status = p_status,
        updated_at = CURRENT_TIMESTAMP
    WHERE id = p_customer_id;

    RETURN FOUND; -- بيرجع true لو فيه صف فعلاً اتحدث
END;
$$;


ALTER FUNCTION public.fn_update_customer(p_customer_id bigint, p_name character varying, p_phone_number character varying, p_email character varying, p_status integer) OWNER TO postgres;

--
-- Name: fn_update_customer_status(bigint, integer, integer); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.fn_update_customer_status(p_customer_id bigint, p_new_status integer, p_old_status integer) RETURNS boolean
    LANGUAGE plpgsql
    AS $$
BEGIN
    -- تحديث حالة العميل والوقت
    UPDATE customers
    SET status = p_new_status,
        updated_at = CURRENT_TIMESTAMP
    WHERE id = p_customer_id
      AND status = p_old_status;

    -- ترجع TRUE لو تم تحديث صف، FALSE لو لا
    RETURN FOUND;
END;
$$;


ALTER FUNCTION public.fn_update_customer_status(p_customer_id bigint, p_new_status integer, p_old_status integer) OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: AspNetRoleClaims; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."AspNetRoleClaims" (
    "Id" integer NOT NULL,
    "RoleId" text NOT NULL,
    "ClaimType" text,
    "ClaimValue" text
);


ALTER TABLE public."AspNetRoleClaims" OWNER TO postgres;

--
-- Name: AspNetRoleClaims_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public."AspNetRoleClaims" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AspNetRoleClaims_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AspNetRoles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."AspNetRoles" (
    "Id" text NOT NULL,
    "Name" character varying(256),
    "NormalizedName" character varying(256),
    "ConcurrencyStamp" text
);


ALTER TABLE public."AspNetRoles" OWNER TO postgres;

--
-- Name: AspNetUserClaims; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."AspNetUserClaims" (
    "Id" integer NOT NULL,
    "UserId" text NOT NULL,
    "ClaimType" text,
    "ClaimValue" text
);


ALTER TABLE public."AspNetUserClaims" OWNER TO postgres;

--
-- Name: AspNetUserClaims_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public."AspNetUserClaims" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AspNetUserClaims_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AspNetUserLogins; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."AspNetUserLogins" (
    "LoginProvider" text NOT NULL,
    "ProviderKey" text NOT NULL,
    "ProviderDisplayName" text,
    "UserId" text NOT NULL
);


ALTER TABLE public."AspNetUserLogins" OWNER TO postgres;

--
-- Name: AspNetUserRoles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."AspNetUserRoles" (
    "UserId" text NOT NULL,
    "RoleId" text NOT NULL
);


ALTER TABLE public."AspNetUserRoles" OWNER TO postgres;

--
-- Name: AspNetUserTokens; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."AspNetUserTokens" (
    "UserId" text NOT NULL,
    "LoginProvider" text NOT NULL,
    "Name" text NOT NULL,
    "Value" text
);


ALTER TABLE public."AspNetUserTokens" OWNER TO postgres;

--
-- Name: AspNetUsers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."AspNetUsers" (
    "Id" text NOT NULL,
    "UserName" character varying(256),
    "NormalizedUserName" character varying(256),
    "Email" character varying(256),
    "NormalizedEmail" character varying(256),
    "EmailConfirmed" boolean NOT NULL,
    "PasswordHash" text,
    "SecurityStamp" text,
    "ConcurrencyStamp" text,
    "PhoneNumber" text,
    "PhoneNumberConfirmed" boolean NOT NULL,
    "TwoFactorEnabled" boolean NOT NULL,
    "LockoutEnd" timestamp with time zone,
    "LockoutEnabled" boolean NOT NULL,
    "AccessFailedCount" integer NOT NULL
);


ALTER TABLE public."AspNetUsers" OWNER TO postgres;

--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


ALTER TABLE public."__EFMigrationsHistory" OWNER TO postgres;

--
-- Name: bookings; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.bookings (
    booking_id bigint NOT NULL,
    appointment_time timestamp with time zone NOT NULL,
    call_id character varying(100) NOT NULL,
    agent_id character varying(100),
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now(),
    status integer DEFAULT 1,
    customer_id bigint NOT NULL,
    CONSTRAINT bookings_status_check CHECK ((status = ANY (ARRAY[0, 1, 2])))
);


ALTER TABLE public.bookings OWNER TO postgres;

--
-- Name: bookings_booking_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.bookings_booking_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.bookings_booking_id_seq OWNER TO postgres;

--
-- Name: bookings_booking_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.bookings_booking_id_seq OWNED BY public.bookings.booking_id;


--
-- Name: customers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.customers (
    id bigint NOT NULL,
    name character varying(255) NOT NULL,
    phone_number character varying(50) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    email character varying(255),
    status integer DEFAULT 0,
    CONSTRAINT customers_status_check CHECK ((status = ANY (ARRAY[0, 1, 2, 3, 4, 5])))
);


ALTER TABLE public.customers OWNER TO postgres;

--
-- Name: customers_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.customers ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.customers_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: bookings booking_id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.bookings ALTER COLUMN booking_id SET DEFAULT nextval('public.bookings_booking_id_seq'::regclass);


--
-- Data for Name: AspNetRoleClaims; Type: TABLE DATA; Schema: public; Owner: postgres
--



--
-- Data for Name: AspNetRoles; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES ('569502c9-2116-46fa-931f-98b43a509279', 'Admin', 'ADMIN', NULL);
INSERT INTO public."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp") VALUES ('fd3aee5e-7bbc-4553-9311-3814e9954282', 'User', 'USER', NULL);


--
-- Data for Name: AspNetUserClaims; Type: TABLE DATA; Schema: public; Owner: postgres
--



--
-- Data for Name: AspNetUserLogins; Type: TABLE DATA; Schema: public; Owner: postgres
--



--
-- Data for Name: AspNetUserRoles; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public."AspNetUserRoles" ("UserId", "RoleId") VALUES ('d69a6c32-2f73-4e34-88e0-601c090f7948', '569502c9-2116-46fa-931f-98b43a509279');
INSERT INTO public."AspNetUserRoles" ("UserId", "RoleId") VALUES ('5de4eb15-b46d-41d4-b8cb-8fac41c9875b', '569502c9-2116-46fa-931f-98b43a509279');


--
-- Data for Name: AspNetUserTokens; Type: TABLE DATA; Schema: public; Owner: postgres
--



--
-- Data for Name: AspNetUsers; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public."AspNetUsers" ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount") VALUES ('d69a6c32-2f73-4e34-88e0-601c090f7948', 'heshamAhmedd', 'HESHAMAHMEDD', 'heshamAhmed@gmail.com', 'HESHAMAHMED@GMAIL.COM', true, 'AQAAAAIAAYagAAAAEJjXMon5bR1kWMo7Hk8m5vo/LbDKZy+TQ0CGZo8fZJwHYwyS8AUvLOy1PX0AXutqXg==', 'EMG7PZVZ3A6E4WXF4RZXD7J6ZJAABTNB', '953eba4d-e92e-4198-9cff-d67c81e1099b', '+83947525433', false, false, NULL, true, 0);
INSERT INTO public."AspNetUsers" ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount") VALUES ('5de4eb15-b46d-41d4-b8cb-8fac41c9875b', 'MohamedElmonier', 'MOHAMEDELMONIER', 'melmonyar@gmail.com', 'MELMONYAR@GMAIL.COM', true, 'AQAAAAIAAYagAAAAEFXlokDSz0YyaXC++dBzuaOqqgD/JY7cViNLtfhgMwyWYfGr0/XiRdsbfRY2BKnvFQ==', 'W7VS4P4P6EEXRDH6TT4NDC3XGWIFYRDE', 'c4e0e0c3-c8be-4c2e-9c05-5aa14f3f8b2d', '015522299280', false, false, NULL, true, 0);


--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20251027001707_InitialIdentitySchema', '9.0.10');


--
-- Data for Name: bookings; Type: TABLE DATA; Schema: public; Owner: postgres
--



--
-- Data for Name: customers; Type: TABLE DATA; Schema: public; Owner: postgres
--



--
-- Name: AspNetRoleClaims_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."AspNetRoleClaims_Id_seq"', 1, false);


--
-- Name: AspNetUserClaims_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."AspNetUserClaims_Id_seq"', 1, false);


--
-- Name: bookings_booking_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.bookings_booking_id_seq', 1, false);


--
-- Name: customers_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.customers_id_seq', 1, false);


--
-- Name: AspNetRoleClaims PK_AspNetRoleClaims; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetRoleClaims"
    ADD CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id");


--
-- Name: AspNetRoles PK_AspNetRoles; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetRoles"
    ADD CONSTRAINT "PK_AspNetRoles" PRIMARY KEY ("Id");


--
-- Name: AspNetUserClaims PK_AspNetUserClaims; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetUserClaims"
    ADD CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id");


--
-- Name: AspNetUserLogins PK_AspNetUserLogins; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetUserLogins"
    ADD CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey");


--
-- Name: AspNetUserRoles PK_AspNetUserRoles; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetUserRoles"
    ADD CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId");


--
-- Name: AspNetUserTokens PK_AspNetUserTokens; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetUserTokens"
    ADD CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name");


--
-- Name: AspNetUsers PK_AspNetUsers; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetUsers"
    ADD CONSTRAINT "PK_AspNetUsers" PRIMARY KEY ("Id");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: bookings bookings_call_id_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.bookings
    ADD CONSTRAINT bookings_call_id_key UNIQUE (call_id);


--
-- Name: bookings bookings_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.bookings
    ADD CONSTRAINT bookings_pkey PRIMARY KEY (booking_id);


--
-- Name: customers customers_phone_number_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_phone_number_key UNIQUE (phone_number);


--
-- Name: customers customers_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_pkey PRIMARY KEY (id);


--
-- Name: EmailIndex; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "EmailIndex" ON public."AspNetUsers" USING btree ("NormalizedEmail");


--
-- Name: IX_AspNetRoleClaims_RoleId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON public."AspNetRoleClaims" USING btree ("RoleId");


--
-- Name: IX_AspNetUserClaims_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_AspNetUserClaims_UserId" ON public."AspNetUserClaims" USING btree ("UserId");


--
-- Name: IX_AspNetUserLogins_UserId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_AspNetUserLogins_UserId" ON public."AspNetUserLogins" USING btree ("UserId");


--
-- Name: IX_AspNetUserRoles_RoleId; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "IX_AspNetUserRoles_RoleId" ON public."AspNetUserRoles" USING btree ("RoleId");


--
-- Name: RoleNameIndex; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "RoleNameIndex" ON public."AspNetRoles" USING btree ("NormalizedName");


--
-- Name: UserNameIndex; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX "UserNameIndex" ON public."AspNetUsers" USING btree ("NormalizedUserName");


--
-- Name: idx_bookings_appointment_time; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_bookings_appointment_time ON public.bookings USING btree (appointment_time);


--
-- Name: idx_bookings_customer_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_bookings_customer_id ON public.bookings USING btree (customer_id);


--
-- Name: idx_customers_email_unique; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX idx_customers_email_unique ON public.customers USING btree (lower((email)::text)) WHERE (email IS NOT NULL);


--
-- Name: idx_customers_phone_number; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_customers_phone_number ON public.customers USING btree (phone_number);


--
-- Name: AspNetRoleClaims FK_AspNetRoleClaims_AspNetRoles_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetRoleClaims"
    ADD CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES public."AspNetRoles"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserClaims FK_AspNetUserClaims_AspNetUsers_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetUserClaims"
    ADD CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES public."AspNetUsers"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserLogins FK_AspNetUserLogins_AspNetUsers_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetUserLogins"
    ADD CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES public."AspNetUsers"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserRoles FK_AspNetUserRoles_AspNetRoles_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetUserRoles"
    ADD CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES public."AspNetRoles"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserRoles FK_AspNetUserRoles_AspNetUsers_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetUserRoles"
    ADD CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES public."AspNetUsers"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserTokens FK_AspNetUserTokens_AspNetUsers_UserId; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."AspNetUserTokens"
    ADD CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES public."AspNetUsers"("Id") ON DELETE CASCADE;


--
-- Name: bookings fk_bookings_customer; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.bookings
    ADD CONSTRAINT fk_bookings_customer FOREIGN KEY (customer_id) REFERENCES public.customers(id) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- PostgreSQL database dump complete
--

\unrestrict 0gvKwstbKwBvoVRUHXP9xdJrKEObkB3bNDEw3dUSWWsXaAykewnDmqR5ybSNBPs

