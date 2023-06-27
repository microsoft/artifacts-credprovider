for CHANGE in "activate" "deactivate"
do
    mkdir -p "${PREFIX}/etc/conda/${CHANGE}.d"
    cp "${SRC_DIR}/src/${CHANGE}/*" "${PREFIX}/etc/conda/${CHANGE}.d/"
done