CREATE TABLE IF NOT EXISTS `pt_servers` (
  `id`         INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  `ip`         VARCHAR(45)       NOT NULL,
  `port`       SMALLINT UNSIGNED NOT NULL,
  `hostname`   VARCHAR(128)      NOT NULL DEFAULT '',
  `first_seen` DATETIME          NOT NULL,
  `last_seen`  DATETIME          NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_server` (`ip`, `port`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `pt_players` (
  `steamid64`     BIGINT UNSIGNED NOT NULL,
  `name`          VARCHAR(128)    NOT NULL DEFAULT '',
  `first_seen`    DATETIME        NOT NULL,
  `last_seen`     DATETIME        NOT NULL,
  `total_seconds` INT UNSIGNED    NOT NULL DEFAULT 0,
  `ct_seconds`    INT UNSIGNED    NOT NULL DEFAULT 0,
  `te_seconds`    INT UNSIGNED    NOT NULL DEFAULT 0,
  `spec_seconds`  INT UNSIGNED    NOT NULL DEFAULT 0,
  PRIMARY KEY (`steamid64`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `pt_playtime` (
  `steamid64`     BIGINT UNSIGNED NOT NULL,
  `server_id`     INT UNSIGNED    NOT NULL,
  `total_seconds` INT UNSIGNED    NOT NULL DEFAULT 0,
  `ct_seconds`    INT UNSIGNED    NOT NULL DEFAULT 0,
  `te_seconds`    INT UNSIGNED    NOT NULL DEFAULT 0,
  `spec_seconds`  INT UNSIGNED    NOT NULL DEFAULT 0,
  `last_seen`     DATETIME        NOT NULL,
  PRIMARY KEY (`steamid64`, `server_id`),
  KEY `idx_server` (`server_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `pt_sessions` (
  `id`               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `steamid64`        BIGINT UNSIGNED NOT NULL,
  `server_id`        INT UNSIGNED    NOT NULL,
  `started_utc`      DATETIME        NOT NULL,
  `ended_utc`        DATETIME        NULL,
  `duration_seconds` INT UNSIGNED    NOT NULL DEFAULT 0,
  `end_reason`       VARCHAR(32)     NULL,
  PRIMARY KEY (`id`),
  KEY `idx_player` (`steamid64`),
  KEY `idx_server` (`server_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
