if not exist %PREFIX%\etc\conda\activate.d mkdir %PREFIX%\etc\conda\activate.d
copy %SRC_DIR%\src\*.* %PREFIX%\etc\conda\activate.d\