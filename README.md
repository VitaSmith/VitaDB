VitaDB - A Database for PS Vita content
=======================================

Description
-----------

This is a database of Vita content, that includes pretty much of all the games, applications, DLCs, updates, etc. that
are currently publicly known, and that is provided in the easy-to-work-with and universally supported SQLite format, as
well as a Windows/Linux/MacOS .NET Core 2.0 application that demonstrates how the database can be manipulated.

### Target Audience

This project is intended for developers or web site creators, who may be interested in building applications that
revolve around the provision of Vita content. Like a library (`.dll`, `.so`) it is not meant to be used directly by
end-users.

### Purpose

The availability of this database can, we hope, help with many new possibilities.

One would be the recreation of one's legally owned content in a manner that is both a lot more convenient and much much
faster than what can be achieved through Sony's very limited content management application as well as its annoyingly
non-user friendly and __sloooooow__ proprietary memory card format.

Another would be with the cataloguing and updating of public Vita content, using PSDLE data, through a web service.

Yet another possibility would be cataloguing applications that can either run on the Vita itself or from a PC with
access to the Vita memory card.

All in all, we hope that this database can become the de-facto, up-to-date repository for any application that needs to
process Vita content.

### About NoNpDrm and zRIF content

You will __NOT__ find any zRIFs (licenses) in the data provided in this project because its goal is to keep track of
official PS Vita content only.

Still, for people who might want to back up their own content licenses using NoNpDrm, the database does provides a
placeholder for zRIFs, and the application also demonstrates how zRIF data can be imported. But that's about it.

Usage
-----

You must have sorted out to run .NET Core 2.0 applications on your machine. If you don't know how, please google it.

```
Usage: VitaDB [OPTIONS]

Options:
  -m, --maintenance          perform database maintenance
  -i, --input=VALUE          name of the input file or URL
  -o, --output=VALUE         name of the output file
  -c, --csv                  import/export CSV
  -n, --nps                  import data from NoPayStation online spreadsheet
      --chihiro              refresh db from Chihiro
      --psn                  refresh db from PSN
  -d, --dump                 dump database to SQL (requires sqlite3.exe)
  -p, --purge                purge/create a new PKG cache dictionary
  -u, --url=VALUE            update DB from PSN Store/Pkg URL(s)
      --version              display version and exit
  -v                         increase verbosity
  -z, --zrif                 import/export zRIFs
  -w, --wait-for-key         wait for keypress before exiting
  -h, --help                 show this message and exit
```

Examples:

```
dotnet VitaDB.dll -n
dotnet VitaDB.dll -u http://zeus.dl.playstation.net/cdn/EP9000/PCSF00001_00/ZGkyvUJatRjaXSqnOaeeFYBJaLXASkjBNWaelzTUYxjuxAtLGYyPvStdjDpYsUKK.pkg
dotnet VitaDB.dll -u https://store.playstation.com/#!/en-gb/games/uncharted-golden-abyss-(eng-pol-por-rus-sca-spa)/cid=EP9000-PCSF00001_00-0000000000000000
dotnet VitaDB.dll -u url_list.txt
dotnet VitaDB.dll -c -i nps_organized.csv
dotnet VitaDB.dll --chihiro
dotnet VitaDB.dll -m
```

Once the database has been created/updated, you can use an SQLite local browser, such as the multiplatform one from http://sqlitebrowser.org/,
to view the data.

You can also edit the content of the `.ini` file according to your requirements.

Database structure
------------------

### Apps

* `TITLE_ID` The 9 alphanumeric character identifier of a title.
   Note that the first 4 characters also identify the region (you can see the region table in the `ini` file).
   Because `TITLE_ID` is always contained in `CONTENT_ID`, this column is redundant, but provided for convenience.
* `NAME` The __official__ name of the game, as per the Sony servers (including any typos and ® or ™, but trimmed
   of leading or trailing white spaces and LFs)
* `ALT_NAME` A user defined alternate name. This can be used, for instance, for the English translation of a
   Japanese title.
* `CONTENT_ID` __THE__ identifier of a published App/Game/Addon/Theme/Avatar. This is what Sony uses to __uniquely__
   identify content, so we do too. As such, this is the table's _primary key_. As mentioned earlier, `CONTENT_ID`
   always contains the `TITLE_ID`.
* `PARENT_ID` The `CONTENT_ID` of any parent application. For instance, `PARENT_ID` can be used with an App/Game to
   link to the `CONTENT_ID` of a bundle (e.g a PS3 or PS4 multiplatform purchase) or, for DLCs, to the `CONTENT_ID`
   of the App the DLC applies to.
* `CATEGORY` An integer representing the application category. See the __Categories__ table below.
* `PKG_ID` The optional `ID` of a PKG entry from the Pkgs table. This is basically used to access the official URL
   where one can download content from the Sony servers.
* `ZRIF` A __PLACEHOLDER__ field for zRIFs. This is provided as a placeholder so that applications that use this DB
   can use this field to store your __legally owned__ licenses, in your personal copy of the DB.
* `COMMENTS` A placeholder for comments.
* `FLAGS` A set of binary flags, that are used to set specific fields to read-only, so that they don't get altered
   when importing data, or to indicate if an application is a free. See the __Flags__ table below for details.

### Pkgs

* `ID` An auto incremented integer ID to uniquely identify each pkg link. This is done so that we don't have to
   use the very long URLs as primary keys.
* `URL` A unique PKG URl. The URLs can indiscriminately be for games, apps, DLC, themes, patches, etc.
* `SIZE` The size of the PKG as read from the servers. You should use a 64-bit integer to map this, as it can be more
   than 4 GB.
* `SHA1` The hex representation of the Pkg SHA1 (last 20 bytes from end-0x20). For convenience, this is being stored
   as a string (that needs to be converted) rather than a byte array.
* `CATEGORY` A short sequence of letters describing the pkg category. This data mostly comes from the SFO.
* `APP_VER` The application version, represented as the decimal number major *100 + minor (e.g. `100` for `v1.0`)
* `SYS_VER` The minimum required system version, represented as the decimal number major *100 + minor (e.g. `355` for `v3.55`)
* `C_DATE` The date when the application was created, in `YYYYMMDD` format. This data comes from the SFO.
* `V_DATE` The date when the PKG was last retrieved, in `YYYYMMDD` format.
* `COMMENTS` A placeholder for comments.

### Updates

* `CONTENT_ID` The application this update applies to.
* `VERSION` The application version, represented as the decimal number major *100 + minor (e.g. `210` for `v2.10`)
* `TYPE` The type of update. See the __Types__ table below.
* `PKG_ID` The `ID` of a update PKG entry in the Pkgs table. This is the primary key.

## Categories

This table contains the values used for `CATEGORY` used by the __Apps__ table.

The values are organized so that Games/Apps should have a value that is < 100, DLC < 200, Themes < 300, etc.
This is done so that searching for Games/Apps, DLC and so on, in the Apps table, can be carried out very easily.

## Flags

This table contains the values used for `FLAGS` in the __Apps__ table. Each value is a power of 2 that, if
found in the `FLAGS` column, indicates whether a specific Flag is active or not. The `_RO` values are for
flags indicating whether a column should be read-only, whereas `FREE_APP` indicates applications that don't
need a license to run.

### Types

This is used by the __Updates__ table to denote the type of update. There are currently only 3 types: `cumulative`,
`incremental` and `hybrid`, which are pretty much a direct mapping of the types reported by the Sony update XML data.

FAQ
---

### Why store the SHA-1?

Besides being useful to check for pkg corruption, this can also be done to detect any silent changes that Sony may
apply to PKG data. For instance, it is not difficult to imagine that, should Sony decide they've had enough of
Henkaku/Enso 3.60 users who download pkg data off their servers, they could relatively easily modify all or a choice
set of pkg they serve with an `eboot` to force a silent system upgrade to 3.61+. 
By storing a copy of the PKG SHA-1, along with the date when that SHA-1 was retrieved, it becomes possible to detect
this kind of "silent upgrade" scenario.

### Why is `CONTENT_ID` used as primary key, rather than `TITLE_ID`?

That's because you can have both a DEMO and FULL game served from same TITLE_ID, with
different PKG urls and CONTENT_ID. For instance:
* 漢字　点つなぎセット (DEMO) = `JP9000-PCSC00038_00-PP2JP00000000001`
* 漢字　点つなぎセット (FULL) = `JP9000-PCSC00038_00-KANJITEN00000001`

### What's the purpose of `PkgCache.json`?

`PkgCache.json` is a locally cached version of a PKG URL to `CONTENT_ID` dictionary.
This avoids having to perform a time consuming round trip to the Sony PKG servers every time we want to validate the
`CONTENT_ID` of a PKG, which is a very frequent operation. The JSON data is simply the serialized version of internal
dictionary we use.

One may consider that this duplicates data that is already present in the database, but it's really the other way
around: this is __external__ data that is needed to validate the consistency of the database, and that we duplicate
locally to avoid having to query the PKG servers.

Note that you can use the `-p` option if you want to recreate your own `PkgCache.json` instead of reusing the one from
the gtihub project.

### Why isn't the SHA-1 column set to be UNIQUE?

Because Sony has found nothing better than provide the exact same content (a 10 GB... patch?!?) for Phantasy Star Online
under 2 different URLs:
* `http://gs.ww.np.dl.playstation.net/ppkg/np/PCSG00141/PCSG00141_T175/7e215919b18a27eb/JP0177-PCSG00141_00-PHANTASYSTARONL2-A0500-V0100-b501028c7b01305693f0b5768dadf2ef71c553b6-PE.pkg`
* `http://gs.ww.np.dl.playstation.net/ppkg/np/PCSG00141/PCSG00141_T176/4f153d40849e6856/JP0177-PCSG00141_00-PHANTASYSTARONL2-A0500-V0100-313ed2cf66018279e89ba59c44db9a7de1e4b287-PE.pkg`
