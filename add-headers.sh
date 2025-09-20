#!/bin/bash

# Script to add LGPL copyright headers to C# files
# Usage: ./add-headers.sh [directory]
# If no directory specified, defaults to 'src'

YEAR=$(date +%Y)
COPYRIGHT_OWNER="MJCZone Inc."
SPDX_LICENSE="LGPL-3.0-or-later"
LICENSE_NAME="GNU Lesser General Public License v3.0 or later"

# Use provided directory or default to 'src'
SEARCH_DIR="${1:-src}"

if [ ! -d "$SEARCH_DIR" ]; then
    echo "Directory '$SEARCH_DIR' does not exist"
    exit 1
fi

# Create the header content
read -r -d '' HEADER << EOF
// Copyright $YEAR $COPYRIGHT_OWNER
// SPDX-License-Identifier: $SPDX_LICENSE
// Licensed under the $LICENSE_NAME.
// See LICENSE in the project root for license information.

EOF

echo "Adding LGPL copyright headers to C# files in '$SEARCH_DIR'..."
echo "Header content:"
echo "$HEADER"

# Find all .cs files and add headers
find "$SEARCH_DIR" -name "*.cs" -type f | while read -r file; do
    # Skip files that already have copyright headers
    if head -5 "$file" | grep -q "Copyright.*$COPYRIGHT_OWNER"; then
        echo "Skipping (already has header): $file"
        continue
    fi

    echo "Adding header to: $file"

    # Create temporary file with header + original content
    {
        echo -n "$HEADER"
        cat "$file"
    } > "$file.tmp"

    # Replace original file
    mv "$file.tmp" "$file"
done

echo "Header addition complete."
echo ""
echo "Remember to:"
echo "1. Review the changes: git diff"
echo "2. Stage the changes: git add ."
echo "3. Commit the changes: git commit -m 'Add copyright headers to new files'"