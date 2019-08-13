# vim:ts=4:et
# ##### BEGIN GPL LICENSE BLOCK #####
#
#  This program is free software; you can redistribute it and/or
#  modify it under the terms of the GNU General Public License
#  as published by the Free Software Foundation; either version 2
#  of the License, or (at your option) any later version.
#
#  This program is distributed in the hope that it will be useful,
#  but WITHOUT ANY WARRANTY; without even the implied warranty of
#  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
#  GNU General Public License for more details.
#
#  You should have received a copy of the GNU General Public License
#  along with this program; if not, write to the Free Software Foundation,
#  Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
#
# ##### END GPL LICENSE BLOCK #####

# <pep8 compliant>

class ScriptError(Exception):
    def __init__(self, fname, line, message):
        Exception.__init__(self, "%s:%d: %s" % (fname, line, message))
        self.line = line

class Script:
    def __init__(self, filename, text, single="{}()':", quotes=True):
        self.filename = filename
        if text[0:3] == "\xef\xbb\xbf":
            text = text[3:]
        self.text = text
        self.single = single
        self.quotes = quotes
        self.pos = 0
        self.line = 1
        self.unget = False
    def error(self, msg):
        raise ScriptError(self.filename, self.line, msg)
    def tokenAvailable(self, crossline=False):
        if self.unget:
            return True
        while self.pos < len(self.text):
            while self.pos < len(self.text) and self.text[self.pos].isspace():
                if self.text[self.pos] == "\n":
                    if not crossline:
                        return False
                    self.line += 1
                self.pos += 1
            if self.pos == len(self.text):
                return False
            if self.text[self.pos] in ["\x1a", "\x04"]:
                # end of file characters
                self.pos += 1
                continue
            if self.text[self.pos:self.pos + 2] == "//":
                while (self.pos < len(self.text)
                       and self.text[self.pos] != "\n"):
                    self.pos += 1
                if self.pos == len(self.text):
                    return False
                if not crossline:
                    return False
                continue
            return True
        return False
    def getLine(self):
        start = self.pos
        end = start
        while self.pos < len(self.text):
            if self.text[self.pos] == "\n":
                self.line += 1
                self.pos += 1
                break
            if self.text[self.pos:self.pos + 2] == "//":
                break
            self.pos += 1
            end = self.pos
        if self.unget:
            self.unget = False
            self.token = self.token + self.text[start:end]
        else:
            self.token = self.text[start:end]
        return self.pos < len(self.text)
    def getToken(self, crossline=False):
        if self.unget:
            self.unget = False
            return self.token
        if not self.tokenAvailable(crossline):
            if not crossline:
                self.error("line is incomplete")
            return None
        if self.quotes and self.text[self.pos] == "\"":
            self.pos += 1
            start = self.pos
            if self.text[self.pos] == len(self.text):
                self.error("EOF inside quoted string")
            while self.text[self.pos] != "\"":
                if self.pos == len(self.text):
                    self.error("EOF inside quoted string")
                    return None
                if self.text[self.pos] == "\n":
                    self.line += 1
                self.pos += 1
            self.token = self.text[start:self.pos]
            self.pos += 1
        else:
            start = self.pos
            if self.text[self.pos] in self.single:
                self.pos += 1
            else:
                while (self.pos < len(self.text)
                       and not self.text[self.pos].isspace()
                       and self.text[self.pos] not in self.single):
                    self.pos += 1
            self.token = self.text[start:self.pos]
        return self.token
    def ungetToken(self):
        self.unget = True
