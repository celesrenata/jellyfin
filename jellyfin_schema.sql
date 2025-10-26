-- Jellyfin PostgreSQL Schema Creation Script
-- Converted from SQLite migrations

CREATE SCHEMA IF NOT EXISTS jellyfin;


CREATE TABLE IF NOT EXISTS jellyfin."ActivityLogs" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(512) NOT NULL,
    "Overview" VARCHAR(512),
    "ShortOverview" VARCHAR(512),
    "Type" VARCHAR(256) NOT NULL,
    "UserId" UUID NOT NULL,
    "ItemId" VARCHAR(256),
    "DateCreated" TIMESTAMP NOT NULL,
    "LogSeverity" INTEGER NOT NULL,
    "RowVersion" INTEGER NOT NULL
);


CREATE TABLE IF NOT EXISTS jellyfin."Users" (
    "Id" UUID PRIMARY KEY,
    "Username" VARCHAR(255) NOT NULL,
    "Password" VARCHAR(65535),
    "EasyPassword" VARCHAR(65535),
    "MustUpdatePassword" BOOLEAN NOT NULL,
    "AudioLanguagePreference" VARCHAR(255),
    "AuthenticationProviderId" VARCHAR(255) NOT NULL,
    "PasswordResetProviderId" VARCHAR(255) NOT NULL,
    "InvalidLoginAttemptCount" INTEGER NOT NULL,
    "LastActivityDate" TIMESTAMP,
    "LastLoginDate" TIMESTAMP,
    "LoginAttemptsBeforeLockout" INTEGER,
    "SubtitleMode" INTEGER NOT NULL,
    "PlayDefaultAudioTrack" BOOLEAN NOT NULL,
    "SubtitleLanguagePreference" VARCHAR(255),
    "DisplayMissingEpisodes" BOOLEAN NOT NULL,
    "DisplayCollectionsView" BOOLEAN NOT NULL,
    "EnableLocalPassword" BOOLEAN NOT NULL,
    "HidePlayedInLatest" BOOLEAN NOT NULL,
    "RememberAudioSelections" BOOLEAN NOT NULL,
    "RememberSubtitleSelections" BOOLEAN NOT NULL,
    "EnableNextEpisodeAutoPlay" BOOLEAN NOT NULL,
    "EnableAutoLogin" BOOLEAN NOT NULL,
    "EnableUserPreferenceAccess" BOOLEAN NOT NULL,
    "MaxParentalAgeRating" INTEGER,
    "RemoteClientBitrateLimit" INTEGER,
    "InternalId" BIGINT NOT NULL,
    "SyncPlayAccess" INTEGER NOT NULL,
    "RowVersion" INTEGER NOT NULL
);


CREATE TABLE IF NOT EXISTS jellyfin."ImageInfos" (
    "Id" SERIAL PRIMARY KEY,
    "ItemId" UUID NOT NULL,
    "Type" INTEGER NOT NULL,
    "Path" TEXT NOT NULL,
    "DateModified" TIMESTAMP NOT NULL,
    "Width" INTEGER NOT NULL,
    "Height" INTEGER NOT NULL,
    "BlurHash" VARCHAR(255),
    "RowVersion" INTEGER NOT NULL
);


CREATE TABLE IF NOT EXISTS jellyfin."DisplayPreferences" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" UUID NOT NULL,
    "ItemId" VARCHAR(255) NOT NULL,
    "Client" VARCHAR(32) NOT NULL,
    "ShowSidebar" BOOLEAN NOT NULL,
    "ShowBackdrop" BOOLEAN NOT NULL,
    "ScrollDirection" INTEGER NOT NULL,
    "IndexBy" VARCHAR(255),
    "SkipForwardLength" INTEGER NOT NULL,
    "SkipBackwardLength" INTEGER NOT NULL,
    "ChromecastVersion" INTEGER NOT NULL,
    "EnableNextVideoInfoOverlay" BOOLEAN NOT NULL,
    "DashboardTheme" VARCHAR(32),
    "TvHome" VARCHAR(32),
    "HomeSections" TEXT,
    "RowVersion" INTEGER NOT NULL
);


CREATE TABLE IF NOT EXISTS jellyfin."Permissions" (
    "Id" SERIAL PRIMARY KEY,
    "Kind" INTEGER NOT NULL,
    "Value" BOOLEAN NOT NULL,
    "RowVersion" INTEGER NOT NULL,
    "UserId" UUID NOT NULL
);


CREATE TABLE IF NOT EXISTS jellyfin."Preferences" (
    "Id" SERIAL PRIMARY KEY,
    "Kind" INTEGER NOT NULL,
    "Value" VARCHAR(65535) NOT NULL,
    "RowVersion" INTEGER NOT NULL,
    "UserId" UUID NOT NULL
);


CREATE TABLE IF NOT EXISTS jellyfin."AccessSchedules" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" UUID NOT NULL,
    "DayOfWeek" INTEGER NOT NULL,
    "StartHour" DOUBLE PRECISION NOT NULL,
    "EndHour" DOUBLE PRECISION NOT NULL
);


CREATE TABLE IF NOT EXISTS jellyfin."Devices" (
    "Id" SERIAL PRIMARY KEY,
    "DeviceId" VARCHAR(255) NOT NULL,
    "DeviceName" VARCHAR(255) NOT NULL,
    "UserId" UUID NOT NULL,
    "AccessToken" VARCHAR(255) NOT NULL,
    "IsActive" BOOLEAN NOT NULL,
    "DateCreated" TIMESTAMP NOT NULL,
    "DateModified" TIMESTAMP NOT NULL,
    "DateLastActivity" TIMESTAMP NOT NULL
);

-- Create indexes
CREATE INDEX IF NOT EXISTS "IX_ActivityLogs_DateCreated" ON jellyfin."ActivityLogs" ("DateCreated");
CREATE INDEX IF NOT EXISTS "IX_Users_Username" ON jellyfin."Users" ("Username");
CREATE INDEX IF NOT EXISTS "IX_DisplayPreferences_UserId" ON jellyfin."DisplayPreferences" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_Permissions_UserId" ON jellyfin."Permissions" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_Preferences_UserId" ON jellyfin."Preferences" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_AccessSchedules_UserId" ON jellyfin."AccessSchedules" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_Devices_DeviceId" ON jellyfin."Devices" ("DeviceId");
CREATE INDEX IF NOT EXISTS "IX_Devices_UserId" ON jellyfin."Devices" ("UserId");

-- Add foreign key constraints
ALTER TABLE jellyfin."Permissions" ADD CONSTRAINT "FK_Permissions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES jellyfin."Users" ("Id") ON DELETE CASCADE;
ALTER TABLE jellyfin."Preferences" ADD CONSTRAINT "FK_Preferences_Users_UserId" FOREIGN KEY ("UserId") REFERENCES jellyfin."Users" ("Id") ON DELETE CASCADE;
ALTER TABLE jellyfin."AccessSchedules" ADD CONSTRAINT "FK_AccessSchedules_Users_UserId" FOREIGN KEY ("UserId") REFERENCES jellyfin."Users" ("Id") ON DELETE CASCADE;
ALTER TABLE jellyfin."Devices" ADD CONSTRAINT "FK_Devices_Users_UserId" FOREIGN KEY ("UserId") REFERENCES jellyfin."Users" ("Id") ON DELETE CASCADE;
