for %%F in (activate deactivate) DO (
    if not exist %PREFIX%\etc\conda\%%F.d mkdir %PREFIX%\etc\conda\%%F.d
    copy %SRC_DIR%\src\%%F\*.* %PREFIX%\etc\conda\%%F.d\
)